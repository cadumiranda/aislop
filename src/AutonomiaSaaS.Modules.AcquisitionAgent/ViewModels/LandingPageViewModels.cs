using System.ComponentModel.DataAnnotations;
using AutonomiaSaaS.Modules.BusinessCore.Domain;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.ViewModels;

public sealed class BusinessDescriptionInputViewModel
{
    [Required(ErrorMessage = "Descreva seu negócio antes de continuar.")]
    [MinLength(20, ErrorMessage = "Descreva seu negócio com um pouco mais de detalhe (mínimo 20 caracteres).")]
    [MaxLength(2000)]
    public string BusinessDescription { get; set; } = string.Empty;
}

/// <summary>
/// Espelha GenerateLandingPageResult (Domain), mas em forma de ViewModel —
/// mesma razão de sempre: a View não deve depender diretamente de um tipo
/// de domínio que pode mudar por motivos que não têm nada a ver com a
/// apresentação.
/// </summary>
public sealed class GenerateLandingPageResultViewModel
{
    public required long LandingPageTaskId { get; init; }
    public required AgentTaskStatus LandingPageTaskStatus { get; init; }
    public string? StagingUrl { get; init; }
    public long? ProductionApprovalTaskId { get; init; }

    /// <summary>
    /// True quando o deploy em staging não ficou saudável — a View usa isso
    /// para mostrar uma mensagem de erro em vez do link de staging.
    /// </summary>
    public bool Failed => LandingPageTaskStatus == AgentTaskStatus.Revertida;
}
