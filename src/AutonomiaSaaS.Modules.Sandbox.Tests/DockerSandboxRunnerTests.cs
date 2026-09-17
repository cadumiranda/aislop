using AutonomiaSaaS.Modules.Sandbox.Abstractions;
using AutonomiaSaaS.Modules.Sandbox.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutonomiaSaaS.Modules.Sandbox.Tests;

/// <summary>
/// Fake que registra os argumentos recebidos, sem rodar Docker de verdade — mesma técnica que
/// LocalFakeDeploymentClient usa na spec v2.0 (seção 9.1) para testar sem depender da Vercel real.
/// </summary>
public sealed class FakeProcessInvoker : IProcessInvoker
{
    public List<(string FileName, IReadOnlyList<string> Arguments)> Invocations { get; } = new();
    public ProcessInvocationResult NextResult { get; set; } = new()
    {
        ExitCode = 0,
        StandardOutput = string.Empty,
        StandardError = string.Empty,
    };
    public bool ThrowOnInvoke { get; set; }

    public Task<ProcessInvocationResult> InvokeAsync(
        string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        Invocations.Add((fileName, arguments));
        if (ThrowOnInvoke)
        {
            throw new InvalidOperationException("docker daemon indisponível (simulado)");
        }
        return Task.FromResult(NextResult);
    }
}

public sealed class DockerSandboxRunnerTests : IDisposable
{
    private readonly string _workspaceRoot;

    public DockerSandboxRunnerTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "sandbox-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_workspaceRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }

    private DockerSandboxRunner CreateRunner(FakeProcessInvoker invoker, bool retainOnFailure = false)
    {
        var options = new DockerSandboxOptions
        {
            WorkspaceRootPath = _workspaceRoot,
            RetainWorkspaceOnFailureForDebugging = retainOnFailure,
        };
        return new DockerSandboxRunner(invoker, options, NullLogger<DockerSandboxRunner>.Instance);
    }

    [Fact]
    public async Task RunAsync_BuildsDockerArguments_WithReadOnlyAndNoNetworkByDefault()
    {
        var invoker = new FakeProcessInvoker();
        var runner = CreateRunner(invoker);

        var request = new SandboxExecutionRequest
        {
            TaskId = 1L,
            AgentName = "product_agent",
            Image = "autonomiasaas/agent-sandbox-node:latest",
            Command = new[] { "npm", "run", "build" },
        };

        await runner.RunAsync(request);

        var (fileName, args) = Assert.Single(invoker.Invocations);
        Assert.Equal("docker", fileName);
        Assert.Contains("--read-only", args);
        Assert.Contains("--rm", args);
        Assert.Contains("--cap-drop", args);
        Assert.Contains("ALL", args);
        var networkIndex = args.ToList().IndexOf("--network");
        Assert.True(networkIndex >= 0);
        Assert.Equal("none", args[networkIndex + 1]);
    }

    [Fact]
    public async Task RunAsync_DoesNotThrow_WhenContainerNameShorterThanMaxLength()
    {
        var invoker = new FakeProcessInvoker();
        var runner = CreateRunner(invoker);

        var request = new SandboxExecutionRequest
        {
            TaskId = 1L,
            AgentName = "product_agent",
            Image = "img",
            Command = new[] { "true" },
        };

        // Should not throw (previous bug: Substring on too-short string)
        await runner.RunAsync(request);

        var (fileName, args) = Assert.Single(invoker.Invocations);
        Assert.Equal("docker", fileName);
        var list = args.ToList();
        var nameIndex = list.IndexOf("--name");
        Assert.True(nameIndex >= 0, "--name must be present in docker arguments");
        var containerName = list[nameIndex + 1];
        Assert.True(containerName.Length <= 60, "container name must be at most 60 chars");
        Assert.Contains("asaas-sbx-", containerName);
    }

    [Fact]
    public async Task RunAsync_RespectsCustomResourceLimits()
    {
        var invoker = new FakeProcessInvoker();
        var runner = CreateRunner(invoker);

        var request = new SandboxExecutionRequest
        {
            TaskId = 2L,
            AgentName = "product_agent",
            Image = "img",
            Command = new[] { "true" },
            Limits = new SandboxResourceLimits { MemoryLimit = "256m", CpuLimit = 0.5, PidsLimit = 32 },
        };

        await runner.RunAsync(request);

        var (_, args) = invoker.Invocations.Single();
        var list = args.ToList();
        Assert.Equal("256m", list[list.IndexOf("--memory") + 1]);
        Assert.Equal("0.5", list[list.IndexOf("--cpus") + 1]);
        Assert.Equal("32", list[list.IndexOf("--pids-limit") + 1]);
    }

    [Fact]
    public async Task RunAsync_MaterializesInputFiles_BeforeInvokingDocker()
    {
        string? capturedWorkspace = null;
        var invoker = new RecordingProcessInvoker(args =>
        {
            var volumeArg = args.ToList()[args.ToList().IndexOf("--volume") + 1];
            // volumeArg format: "{hostPath}:/workspace:rw"
            // On Windows the hostPath contains a drive letter with ':' which would break naive Split(':')[0].
            // Prefer finding the separator ":/workspace" and taking everything before it.
            var separator = ":/workspace";
            var idx = volumeArg.IndexOf(separator, StringComparison.Ordinal);
            if (idx >= 0)
            {
                capturedWorkspace = volumeArg.Substring(0, idx);
            }
            else
            {
                // Fallback for unexpected formats: lastIndexOf ":" before the container path
                var parts = volumeArg.Split(':');
                if (parts.Length >= 3)
                {
                    // join all parts except the last two (container path and mode)
                    capturedWorkspace = string.Join(":", parts.Take(parts.Length - 2));
                }
                else
                {
                    capturedWorkspace = parts[0];
                }
            }
        });
        var runner = CreateRunner2(invoker);

        var request = new SandboxExecutionRequest
        {
            TaskId = 3L,
            AgentName = "product_agent",
            Image = "img",
            Command = new[] { "cat", "input.txt" },
            InputFiles = new Dictionary<string, string> { ["input.txt"] = "conteudo de teste" },
        };

        await runner.RunAsync(request);

        Assert.NotNull(capturedWorkspace);
        var writtenFile = Path.Combine(capturedWorkspace!, "input.txt");
        Assert.True(File.Exists(writtenFile));
        Assert.Equal("conteudo de teste", await File.ReadAllTextAsync(writtenFile));
    }

    [Fact]
    public async Task RunAsync_RejectsInputFilePath_ThatEscapesWorkspace()
    {
        var invoker = new FakeProcessInvoker();
        var runner = CreateRunner(invoker);

        var request = new SandboxExecutionRequest
        {
            TaskId = 4L,
            AgentName = "product_agent",
            Image = "img",
            Command = new[] { "true" },
            InputFiles = new Dictionary<string, string> { ["../../etc/passwd"] = "malicious" },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(request));
        // Docker nunca deveria ter sido chamado — a validação de path acontece antes.
        Assert.Empty(invoker.Invocations);
    }

    [Fact]
    public async Task RunAsync_ReturnsFailure_WhenExitCodeNonZero_WithoutThrowing()
    {
        var invoker = new FakeProcessInvoker
        {
            NextResult = new ProcessInvocationResult { ExitCode = 1, StandardOutput = "", StandardError = "build falhou" },
        };
        var runner = CreateRunner(invoker);

        var result = await runner.RunAsync(new SandboxExecutionRequest
        {
            TaskId = 5L,
            AgentName = "product_agent",
            Image = "img",
            Command = new[] { "npm", "run", "build" },
        });

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("build falhou", result.StandardError.TrimEnd());
    }

    [Fact]
    public async Task RunAsync_MarksTimedOut_AndAttemptsForceRemoval()
    {
        var invoker = new FakeProcessInvoker
        {
            NextResult = new ProcessInvocationResult { ExitCode = -1, StandardOutput = "", StandardError = "", TimedOut = true },
        };
        var runner = CreateRunner(invoker);

        var result = await runner.RunAsync(new SandboxExecutionRequest
        {
            TaskId = 6L,
            AgentName = "product_agent",
            Image = "img",
            Command = new[] { "sleep", "999" },
            Timeout = TimeSpan.FromMilliseconds(10),
        });

        Assert.True(result.TimedOut);
        Assert.False(result.Succeeded);
        // Uma chamada para o `docker run` original, e uma segunda tentando `docker rm -f`.
        Assert.Equal(2, invoker.Invocations.Count);
        Assert.Contains("rm", invoker.Invocations[1].Arguments);
    }

    [Fact]
    public async Task RunAsync_CleansUpWorkspace_ByDefault()
    {
        var invoker = new FakeProcessInvoker();
        var runner = CreateRunner(invoker, retainOnFailure: false);

        var request = new SandboxExecutionRequest
        {
            TaskId = 7L,
            AgentName = "product_agent",
            Image = "img",
            Command = new[] { "true" },
        };

        await runner.RunAsync(request);

        var expectedWorkspace = Path.Combine(_workspaceRoot, "t-7");
        Assert.False(Directory.Exists(expectedWorkspace));
    }

    private DockerSandboxRunner CreateRunner2(IProcessInvoker invoker)
    {
        var options = new DockerSandboxOptions { WorkspaceRootPath = _workspaceRoot };
        return new DockerSandboxRunner(invoker, options, NullLogger<DockerSandboxRunner>.Instance);
    }

    private sealed class RecordingProcessInvoker : IProcessInvoker
    {
        private readonly Action<IReadOnlyList<string>> _onInvoke;
        public RecordingProcessInvoker(Action<IReadOnlyList<string>> onInvoke) => _onInvoke = onInvoke;

        public Task<ProcessInvocationResult> InvokeAsync(
            string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            _onInvoke(arguments);
            return Task.FromResult(new ProcessInvocationResult { ExitCode = 0, StandardOutput = "", StandardError = "" });
        }
    }
}
