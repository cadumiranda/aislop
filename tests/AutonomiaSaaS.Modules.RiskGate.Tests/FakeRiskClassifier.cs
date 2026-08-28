using AutonomiaSaaS.Modules.RiskGate.Domain;

namespace AutonomiaSaaS.Modules.RiskGate.Tests;

/// <summary>
/// Fake que sempre retorna o resultado configurado, independente do request.
/// Os testes de TaskRiskGateway não precisam exercitar a lógica real de
/// classificação (já coberta por RiskClassifierTests) — só precisam garantir
/// que o gateway reage corretamente a cada RiskLevel possível.
/// </summary>
internal sealed class FakeRiskClassifier : IRiskClassifier
{
    private readonly RiskClassificationResult _result;

    public FakeRiskClassifier(RiskClassificationResult result) => _result = result;

    public Task<RiskClassificationResult> ClassifyAsync(
        RiskClassificationRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(_result);
}
