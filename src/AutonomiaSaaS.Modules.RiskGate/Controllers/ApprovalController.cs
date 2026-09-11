using AutonomiaSaaS.Modules.BusinessCore.Domain;
using AutonomiaSaaS.Modules.BusinessCore.Services;
using AutonomiaSaaS.Modules.RiskGate.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.Security.Permissions;

namespace AutonomiaSaaS.Modules.RiskGate.Controllers;

/// <summary>
/// [Admin] é o atributo do Orchard Core que faz este controller renderizar
/// com o tema do Admin e ficar sob a rota /Admin — é o mesmo padrão usado
/// por qualquer área administrativa nativa do framework.
/// </summary>
[Admin]
public sealed class ApprovalController : Controller
{
    private readonly IAgentTaskStore _agentTaskStore;
    private readonly IApprovedTaskDispatcher _approvedTaskDispatcher;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<ApprovalController> _logger;
    private readonly IStringLocalizer S;

    public ApprovalController(
        IAgentTaskStore agentTaskStore,
        IApprovedTaskDispatcher approvedTaskDispatcher,
        IAuthorizationService authorizationService,
        ILogger<ApprovalController> logger,
        IStringLocalizer<ApprovalController> localizer)
    {
        _agentTaskStore = agentTaskStore;
        _approvedTaskDispatcher = approvedTaskDispatcher;
        _authorizationService = authorizationService;
        _logger = logger;
        S = localizer;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, RiskGatePermissions.ManageApprovals))
        {
            return Forbid();
        }

        var pending = await _agentTaskStore.GetPendingApprovalAsync();

        var viewModel = new ApprovalIndexViewModel
        {
            PendingApprovals = pending.Select(MapToViewModel).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(long taskId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, RiskGatePermissions.ManageApprovals))
        {
            return Forbid();
        }

        var task = await _agentTaskStore.GetByIdAsync(taskId);
        if (task is null)
        {
            return NotFound();
        }

        // Passo 1: transiciona para Aprovada — validado pela máquina de
        // estados dentro de AgentTaskStore.TransitionAsync, não por este
        // controller (seção 6 do documento de arquitetura).
        await _agentTaskStore.TransitionAsync(taskId, AgentTaskStatus.Aprovada);

        // Passo 2: dispara a execução real. Erros do agente específico
        // (ex: promoção para produção falhou e foi revertida) são tratados
        // dentro do próprio executor — aqui só capturamos falhas inesperadas
        // para não derrubar a página do painel com um erro 500 cru.
        try
        {
            var approvedTask = await _agentTaskStore.GetByIdAsync(taskId)
                ?? throw new InvalidOperationException($"AgentTask '{taskId}' sumiu entre a aprovação e o despacho.");

            await _approvedTaskDispatcher.DispatchAsync(approvedTask);

            TempData["SuccessMessage"] = S["Ação aprovada e executada."].Value;
        }
        catch (Exception ex)
        {
            // Log com o taskId é essencial aqui: uma tarefa aprovada mas cuja
            // execução falhou de forma inesperada (não tratada pelo próprio
            // executor) precisa ser rastreável, já que ela fica com Status =
            // Aprovada, tecnicamente "presa" entre aprovação e execução.
            _logger.LogError(ex, "Falha ao despachar execução da tarefa aprovada {TaskId}.", taskId);
            TempData["ErrorMessage"] = S["A tarefa foi aprovada, mas houve um erro ao executá-la. Verifique os logs."].Value;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long taskId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, RiskGatePermissions.ManageApprovals))
        {
            return Forbid();
        }

        var task = await _agentTaskStore.GetByIdAsync(taskId);
        if (task is null)
        {
            return NotFound();
        }

        await _agentTaskStore.TransitionAsync(taskId, AgentTaskStatus.Rejeitada);

        TempData["SuccessMessage"] = S["Ação rejeitada."].Value;

        return RedirectToAction(nameof(Index));
    }

    private static PendingApprovalItemViewModel MapToViewModel(BusinessCore.Parts.AgentTaskPart part)
        => new()
        {
            // ContentItemId vem do ContentItem que é dono da part — ver
            // aviso no README sobre esta linha ser a que tenho menos certeza
            // de estar correta sem compilar contra o Orchard Core real.
            TaskId = part.ContentItem.ContentItemId,
            AgentName = part.AgentName,
            Action = part.Action,
            EstimatedCostTokens = part.EstimatedCostTokens,
            RollbackAction = part.RollbackAction,
            ApprovalTimeout = part.ApprovalTimeout
        };
}
