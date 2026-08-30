using AutonomiaSaaS.Modules.AcquisitionAgent.Domain;
using AutonomiaSaaS.Modules.AcquisitionAgent.Services;
using AutonomiaSaaS.Modules.AcquisitionAgent.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;

namespace AutonomiaSaaS.Modules.AcquisitionAgent.Controllers;

/// <summary>
/// Endpoint que fecha o critério de sucesso da Fase 1 (seção 10 do documento
/// de arquitetura): "usuário descreve o negócio → recebe uma landing page
/// publicada e funcional sem tocar em código". Diferente de ApprovalController
/// (módulo RiskGate), este NÃO tem [Admin] — é para o usuário final do SaaS,
/// não para um administrador da plataforma.
///
/// [Authorize] sem uma Permission específica é uma simplificação deliberada
/// da Fase 1: qualquer usuário autenticado no tenant pode gerar uma landing
/// page para o próprio negócio (cada tenant já é um cliente isolado — não há
/// "outros negócios" para vazar dentro do mesmo tenant). Uma permissão
/// dedicada faria sentido a partir do momento em que um tenant tiver mais de
/// um usuário com papéis diferentes.
/// </summary>
[Authorize]
public sealed class LandingPageController : Controller
{
    private readonly IAcquisitionAgentOrchestrator _orchestrator;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger<LandingPageController> _logger;

    public LandingPageController(
        IAcquisitionAgentOrchestrator orchestrator,
        ShellSettings shellSettings,
        ILogger<LandingPageController> logger)
    {
        _orchestrator = orchestrator;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index() => View(new BusinessDescriptionInputViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(BusinessDescriptionInputViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(Index), model);
        }

        GenerateLandingPageResult result;
        try
        {
            // ShellSettings.Name identifica o tenant atual no Orchard Core —
            // é o TenantId que AgentRuntime usa para associar custo de
            // modelo ao cliente certo (seção 8 do documento de arquitetura).
            result = await _orchestrator.GenerateLandingPageAsync(
                _shellSettings.Name, model.BusinessDescription);
        }
        catch (Exception ex)
        {
            // Falha inesperada ANTES de qualquer AgentTask existir (ex: chave
            // de API da Anthropic ausente/inválida) — não há tarefa para
            // apontar no painel, então isso precisa aparecer direto para o
            // usuário aqui, não só no log.
            _logger.LogError(ex, "Falha ao gerar landing page para o tenant {TenantName}.", _shellSettings.Name);
            ModelState.AddModelError(
                string.Empty, "Não foi possível gerar a landing page agora. Tente novamente em instantes.");
            return View(nameof(Index), model);
        }

        var viewModel = new GenerateLandingPageResultViewModel
        {
            LandingPageTaskId = result.LandingPageTaskId,
            LandingPageTaskStatus = result.LandingPageTaskStatus,
            StagingUrl = result.StagingUrl,
            ProductionApprovalTaskId = result.ProductionApprovalTaskId
        };

        return View("Result", viewModel);
    }
}
