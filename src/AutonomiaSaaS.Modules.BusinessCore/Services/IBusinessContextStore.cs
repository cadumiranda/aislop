using AutonomiaSaaS.Modules.BusinessCore.Parts;
using OrchardCore.ContentManagement;

namespace AutonomiaSaaS.Modules.BusinessCore.Services;

/// <summary>
/// Identificador fixo do único item de BusinessContext deste tenant. Como
/// cada tenant do Orchard Core já é um cliente isolado (ver seção 1 do
/// arquivo de mapeamento na especificação técnica), não precisamos de uma
/// consulta por "qual é o contexto deste business_id" — existe exatamente
/// um, sempre com este slug fixo.
/// </summary>
public interface IBusinessContextStore
{
    Task<BusinessContextPart?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria o contexto se ainda não existir, ou atualiza o existente.
    /// Não há necessidade de uma operação de "criar" separada de "atualizar"
    /// aqui — diferente de AgentTask, BusinessContext não tem máquina de
    /// estados, é só um documento de configuração do tenant.
    /// </summary>
    Task UpsertAsync(Action<BusinessContextPart> configure, CancellationToken cancellationToken = default);
}

public sealed class BusinessContextStore : IBusinessContextStore
{
    private const string FixedContentItemId = "business-context-singleton";

    private readonly IContentManager _contentManager;

    public BusinessContextStore(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public async Task<BusinessContextPart?> GetAsync(CancellationToken cancellationToken = default)
    {
        var contentItem = await _contentManager.GetAsync(FixedContentItemId, VersionOptions.Published);
        return contentItem?.As<BusinessContextPart>();
    }

    public async Task UpsertAsync(
        Action<BusinessContextPart> configure, CancellationToken cancellationToken = default)
    {
        var contentItem = await _contentManager.GetAsync(FixedContentItemId, VersionOptions.Latest);

        if (contentItem is null)
        {
            contentItem = await _contentManager.NewAsync("BusinessContext");
            contentItem.ContentItemId = FixedContentItemId;
            contentItem.Alter<BusinessContextPart>(configure);
            await _contentManager.CreateAsync(contentItem, VersionOptions.Published);
        }
        else
        {
            contentItem.Alter<BusinessContextPart>(configure);
            await _contentManager.UpdateAsync(contentItem);
            await _contentManager.PublishAsync(contentItem);
        }
    }
}
