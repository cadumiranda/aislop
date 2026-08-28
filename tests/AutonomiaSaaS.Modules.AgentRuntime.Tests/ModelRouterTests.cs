using AutonomiaSaaS.Modules.AgentRuntime.ModelRouting;
using Microsoft.Extensions.Options;
using Xunit;

namespace AutonomiaSaaS.Modules.AgentRuntime.Tests;

public class ModelRouterTests
{
    private static ModelRouter CreateRouter(AgentRuntimeOptions? options = null)
    {
        options ??= new AgentRuntimeOptions
        {
            PlanningModel = "claude-sonnet-5",
            RepetitiveModel = "claude-haiku-4-5-20251001"
        };
        return new ModelRouter(Options.Create(options));
    }

    [Fact]
    public void ResolveModel_ComplexidadePlanning_RetornaModeloDePlanejamento()
    {
        var router = CreateRouter();

        var model = router.ResolveModel(ActivityComplexity.Planning);

        Assert.Equal("claude-sonnet-5", model);
    }

    [Fact]
    public void ResolveModel_ComplexidadeRepetitive_RetornaModeloMaisBarato()
    {
        var router = CreateRouter();

        var model = router.ResolveModel(ActivityComplexity.Repetitive);

        Assert.Equal("claude-haiku-4-5-20251001", model);
    }

    [Fact]
    public void ResolveModel_ComplexidadeInvalida_LancaArgumentOutOfRangeException()
    {
        var router = CreateRouter();
        var complexidadeInvalida = (ActivityComplexity)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => router.ResolveModel(complexidadeInvalida));
    }
}
