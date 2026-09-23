using System.Diagnostics;

namespace AutonomiaSaaS.Modules.Sandbox.Execution;

public sealed record ProcessInvocationResult
{
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
    public bool TimedOut { get; init; }
}

/// <summary>
/// Isola o "rodar um processo do SO" atrás de uma interface — mesma razão pela qual o AgentRuntime
/// isola a chamada HTTP à Anthropic atrás de IAnthropicClient (spec v2.0, seção 6): torna o resto do
/// módulo testável sem depender de um Docker daemon real disponível no ambiente de CI.
/// </summary>
public interface IProcessInvoker
{
    Task<ProcessInvocationResult> InvokeAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessInvoker : IProcessInvoker
{
    public async Task<ProcessInvocationResult> InvokeAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            timedOut = true;
            TryKill(process);
        }

        return new ProcessInvocationResult
        {
            ExitCode = timedOut ? -1 : process.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
            TimedOut = timedOut,
        };
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                // entireProcessTree=true: mata também o container caso o `docker run` pai já tenha
                // saído mas o filho continue — cinto e suspensório, o container em si tem --rm.
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Processo já pode ter saído entre a checagem e o Kill — não é uma falha a reportar.
        }
    }
}
