using AutonomiaSaaS.Modules.Sandbox.Abstractions;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;

namespace AutonomiaSaaS.Modules.Sandbox.Execution;

public sealed class DockerSandboxOptions
{
    /// <summary>Diretório base no host onde os workspaces por execução são criados. Cada execução ganha um subdiretório novo (TaskId), removido ao final.</summary>
    public string WorkspaceRootPath { get; set; } = "/var/autonomiasaas/sandbox-workspaces";

    /// <summary>Caminho do binário docker. Configurável porque em alguns hosts é "docker", em outros um caminho absoluto.</summary>
    public string DockerExecutablePath { get; set; } = "docker";

    /// <summary>Se true, o workspace do host não é apagado após a execução (útil para depurar um agente com comportamento estranho).</summary>
    public bool RetainWorkspaceOnFailureForDebugging { get; set; } = true;
}

/// <summary>
/// Runner que invoca `docker run` via linha de comando (ver README para o porquê de não usar
/// Docker.DotNet). Cada chamada a RunAsync é uma execução isolada e efêmera: workspace novo,
/// container novo, container removido ao final (--rm), workspace removido ao final (a menos que
/// tenha falhado e RetainWorkspaceOnFailureForDebugging esteja ligado).
/// </summary>
public sealed class DockerSandboxRunner : ISandboxRunner
{
    private readonly IProcessInvoker _processInvoker;
    private readonly DockerSandboxOptions _options;
    private readonly ILogger<DockerSandboxRunner> _logger;

    public DockerSandboxRunner(
        IProcessInvoker processInvoker,
        DockerSandboxOptions options,
        ILogger<DockerSandboxRunner> logger)
    {
        _processInvoker = processInvoker;
        _options = options;
        _logger = logger;
    }

    public async Task<SandboxExecutionResult> RunAsync(
        SandboxExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspacePath = Path.Combine(_options.WorkspaceRootPath, request.TaskId.ToString());
        Directory.CreateDirectory(workspacePath);

        try
        {
            await MaterializeInputFilesAsync(workspacePath, request.InputFiles, cancellationToken);

            var rawContainerName = $"asaas-sbx-{request.TaskId}-{Guid.NewGuid():N}";
            var containerName = rawContainerName.Length > 60 ? rawContainerName.Substring(0, 60) : rawContainerName;
            var arguments = BuildDockerRunArguments(request, workspacePath, containerName);

            _logger.LogInformation(
                "Sandbox: iniciando execução {TaskId} (agente {AgentName}, imagem {Image}, rede {NetworkMode})",
                request.TaskId, request.AgentName, request.Image, request.NetworkMode);

            var processResult = await _processInvoker.InvokeAsync(
                _options.DockerExecutablePath, arguments, request.Timeout, cancellationToken);

            if (processResult.TimedOut)
            {
                _logger.LogWarning("Sandbox: execução {TaskId} atingiu timeout de {Timeout}", request.TaskId, request.Timeout);
                await TryForceRemoveContainerAsync(containerName, cancellationToken);
                return new SandboxExecutionResult
                {
                    TaskId = request.TaskId,
                    Succeeded = false,
                    ExitCode = -1,
                    StandardOutput = processResult.StandardOutput,
                    StandardError = processResult.StandardError,
                    TimedOut = true,
                    FailureReason = $"Execução excedeu o timeout de {request.Timeout}.",
                };
            }

            var outputFiles = await CaptureOutputFilesAsync(workspacePath, request.OutputPatterns, cancellationToken);

            var succeeded = processResult.ExitCode == 0;
            return new SandboxExecutionResult
            {
                TaskId = request.TaskId,
                Succeeded = succeeded,
                ExitCode = processResult.ExitCode,
                StandardOutput = processResult.StandardOutput,
                StandardError = processResult.StandardError,
                OutputFiles = outputFiles,
                FailureReason = succeeded ? null : $"Comando saiu com código {processResult.ExitCode}.",
            };
        }
        finally
        {
            CleanupWorkspace(workspacePath, keepForDebugging: _options.RetainWorkspaceOnFailureForDebugging);
        }
    }

    private static List<string> BuildDockerRunArguments(
        SandboxExecutionRequest request, string workspacePath, string containerName)
    {
        var args = new List<string>
        {
            "run",
            "--rm",
            "--name", containerName,
            "--network", request.NetworkMode,
            "--memory", request.Limits.MemoryLimit,
            "--cpus", request.Limits.CpuLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--pids-limit", request.Limits.PidsLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--read-only",
            "--tmpfs", "/tmp:rw,size=64m",
            // Único diretório gravável do container: o workspace desta execução, e nada além dele.
            "--volume", $"{workspacePath}:/workspace:rw",
            "--workdir", "/workspace",
            // Defesa em profundidade: mesmo com --network none, remove qualquer capacidade de rede residual.
            "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges",
            request.Image,
        };
        args.AddRange(request.Command);
        return args;
    }

    private static async Task MaterializeInputFilesAsync(
        string workspacePath, IReadOnlyDictionary<string, string> inputFiles, CancellationToken cancellationToken)
    {
        foreach (var (relativePath, content) in inputFiles)
        {
            var fullPath = ResolveWithinWorkspace(workspacePath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content, cancellationToken);
        }
    }

    private static async Task<Dictionary<string, string>> CaptureOutputFilesAsync(
        string workspacePath, IReadOnlyList<string> outputPatterns, CancellationToken cancellationToken)
    {
        var captured = new Dictionary<string, string>();
        if (outputPatterns.Count == 0)
        {
            return captured;
        }

        var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher();
        foreach (var pattern in outputPatterns)
        {
            matcher.AddInclude(pattern);
        }

        var directory = new DirectoryInfoWrapper(new DirectoryInfo(workspacePath));

        var result = matcher.Execute(directory);
        var fullPaths = result.Files
            .Select(f => Path.GetFullPath(Path.Combine(workspacePath, f.Path)))
            .ToList();

        //var matches = matcher..GetResultsInFullPath(workspacePath);
        foreach (var filePath in fullPaths)
        {
            var relativePath = Path.GetRelativePath(workspacePath, filePath);
            captured[relativePath] = await File.ReadAllTextAsync(filePath, cancellationToken);
        }

        return captured;
    }

    /// <summary>
    /// Impede path traversal (ex: "../../etc/passwd") vindo de um InputFiles/OutputPatterns malformado —
    /// nunca escrever fora do workspace da própria execução, mesmo por engano de quem monta o request.
    /// </summary>
    private static string ResolveWithinWorkspace(string workspacePath, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(workspacePath, relativePath));
        var normalizedRoot = Path.GetFullPath(workspacePath) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Caminho de arquivo '{relativePath}' resolve para fora do workspace do sandbox.");
        }
        return fullPath;
    }

    private static string SanitizeForPath(string value)
        => new(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

    private async Task TryForceRemoveContainerAsync(string containerName, CancellationToken cancellationToken)
    {
        try
        {
            await _processInvoker.InvokeAsync(
                _options.DockerExecutablePath,
                new[] { "rm", "-f", containerName },
                TimeSpan.FromSeconds(10),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sandbox: falha ao forçar remoção do container {ContainerName} após timeout", containerName);
        }
    }

    private void CleanupWorkspace(string workspacePath, bool keepForDebugging)
    {
        try
        {
            if (!keepForDebugging)
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sandbox: falha ao limpar workspace {WorkspacePath}", workspacePath);
        }
    }
}
