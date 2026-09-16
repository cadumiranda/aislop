using System.ComponentModel.DataAnnotations;

namespace AutonomiaSaaS.Modules.CredentialVault.Admin.ViewModels;

/// <summary>Uma linha da listagem — espelha CredentialSummary, nunca carrega o valor.</summary>
public sealed class AgentCredentialSummaryViewModel
{
    public required string AgentName { get; init; }
    public required string Key { get; init; }
    public string? Description { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public DateTime? RotatedUtc { get; init; }
}

public sealed class AgentCredentialListViewModel
{
    public IReadOnlyList<AgentCredentialSummaryViewModel> Credentials { get; init; } = Array.Empty<AgentCredentialSummaryViewModel>();
}

/// <summary>
/// Formulário de criação/rotação. Deliberadamente não tem um modo "Edit" que pré-popule Value —
/// só existe "gravar um valor novo", pra chave existente ou nova. Ver README: cofre é write-only.
/// </summary>
public sealed class AgentCredentialEditViewModel
{
    [Required]
    [StringLength(200)]
    [Display(Name = "Agente")]
    public string AgentName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Chave")]
    public string Key { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Valor")]
    public string Value { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Descrição")]
    public string? Description { get; set; }

    /// <summary>
    /// Marca se esta chave já existe (vindo de "Rotacionar" na listagem) — usado só pra UI
    /// (trocar o texto do botão/aviso), nunca pra decidir a lógica de negócio: StoreAsync já
    /// sobrescreve de qualquer jeito, então este campo é puramente cosmético.
    /// </summary>
    public bool IsRotation { get; set; }
}
