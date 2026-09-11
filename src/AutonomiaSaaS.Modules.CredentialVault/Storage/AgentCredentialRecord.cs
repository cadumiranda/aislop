using YesSql.Indexes;

namespace AutonomiaSaaS.Modules.CredentialVault.Storage;

/// <summary>
/// Documento YesSql puro (não Content Type — ver README para o porquê). O valor da credencial
/// aqui SEMPRE está cifrado (ProtectedValue); nunca guarde o texto puro neste registro.
/// </summary>
public sealed class AgentCredentialRecord
{
    public int Id { get; set; }
    public required string AgentName { get; set; }
    public required string Key { get; set; }
    public required string ProtectedValue { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? RotatedUtc { get; set; }
}

/// <summary>Índice de busca por AgentName+Key — mesmo padrão de AgentTaskPartIndex (spec v2.0, seção 3.2).</summary>
public sealed class AgentCredentialIndex : MapIndex
{
    public string AgentName { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}

public sealed class AgentCredentialIndexProvider : IndexProvider<AgentCredentialRecord>
{
    public override void Describe(DescribeContext<AgentCredentialRecord> context)
    {
        context.For<AgentCredentialIndex>()
            .Map(record => new AgentCredentialIndex
            {
                AgentName = record.AgentName,
                Key = record.Key,
            });
    }
}
