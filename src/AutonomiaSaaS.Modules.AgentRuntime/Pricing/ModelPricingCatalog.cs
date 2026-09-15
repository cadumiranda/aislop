namespace AutonomiaSaaS.Modules.AgentRuntime.Pricing;

public sealed record ModelRate
{
    /// <summary>USD por milhão de tokens de entrada.</summary>
    public required decimal InputPerMillionTokens { get; init; }

    /// <summary>USD por milhão de tokens de saída.</summary>
    public required decimal OutputPerMillionTokens { get; init; }
}

/// <summary>
/// Separado de ICostLogger de propósito — ver README ("duas responsabilidades separadas").
/// Trocar a implementação (ex: por uma que lê de appsettings ou de uma tabela editável no
/// admin) não deveria exigir tocar em PersistentCostLogger.
/// </summary>
public interface IModelPricingProvider
{
    /// <summary>
    /// Lança InvalidOperationException se o modelo não estiver no catálogo — silenciar isso e
    /// gravar custo zero esconderia um gasto real, o que é pior do que falhar alto.
    /// </summary>
    ModelRate GetRate(string model);
}

/// <summary>
/// Catálogo estático com valores de exemplo. VERIFIQUE contra
/// https://docs.claude.com/en/docs/about-claude/pricing antes de confiar nisso para qualquer
/// decisão de bloqueio de custo real — ver aviso completo no README deste pacote.
/// </summary>
public sealed class StaticModelPricingCatalog : IModelPricingProvider
{
    private readonly IReadOnlyDictionary<string, ModelRate> _rates;

    public StaticModelPricingCatalog(IReadOnlyDictionary<string, ModelRate>? overrides = null)
    {
        var defaults = new Dictionary<string, ModelRate>(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-haiku-4-5-20251001"] = new ModelRate { InputPerMillionTokens = 1m, OutputPerMillionTokens = 5m },
            ["claude-sonnet-5"] = new ModelRate { InputPerMillionTokens = 2m, OutputPerMillionTokens = 10m },
            ["claude-opus-5"] = new ModelRate { InputPerMillionTokens = 5m, OutputPerMillionTokens = 25m },
        };

        if (overrides is not null)
        {
            foreach (var (model, rate) in overrides)
            {
                defaults[model] = rate;
            }
        }

        _rates = defaults;
    }

    public ModelRate GetRate(string model)
    {
        if (_rates.TryGetValue(model, out var rate))
        {
            return rate;
        }

        throw new InvalidOperationException(
            $"Nenhuma tarifa cadastrada para o modelo '{model}'. Adicione ao catálogo antes de " +
            "registrar custo para ele — gravar custo zero por omissão esconderia gasto real.");
    }
}
