using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AutonomiaSaaS.Modules.Sandbox.Abstractions;

namespace AutonomiaSaaS.Modules.Sandbox.Execution;

/// <summary>
/// Fila em memória (Channel), unbounded — v0.1, single-host. Ver nota em ISandboxExecutionQueue
/// sobre por que isso é aceitável agora e o que fazer quando deixar de ser.
/// </summary>
public sealed class InMemorySandboxExecutionQueue : ISandboxExecutionQueue
{
    private readonly Channel<SandboxExecutionRequest> _channel =
        Channel.CreateUnbounded<SandboxExecutionRequest>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(SandboxExecutionRequest request, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(request, cancellationToken);

    public async IAsyncEnumerable<SandboxExecutionRequest> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return request;
        }
    }
}
