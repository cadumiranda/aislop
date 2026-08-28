using Microsoft.Extensions.Options;

namespace AutonomiaSaaS.Modules.AgentRuntime.ModelRouting;

public sealed class ModelRouter : IModelRouter
{
    private readonly AgentRuntimeOptions _options;

    public ModelRouter(IOptions<AgentRuntimeOptions> options)
    {
        _options = options.Value;
    }

    public string ResolveModel(ActivityComplexity complexity) => complexity switch
    {
        ActivityComplexity.Planning => _options.PlanningModel,
        ActivityComplexity.Repetitive => _options.RepetitiveModel,
        _ => throw new ArgumentOutOfRangeException(
            nameof(complexity), complexity, "Nível de complexidade não mapeado para nenhum modelo.")
    };
}
