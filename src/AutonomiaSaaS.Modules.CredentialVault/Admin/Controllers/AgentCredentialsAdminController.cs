using AutonomiaSaaS.Modules.CredentialVault.Abstractions;
using AutonomiaSaaS.Modules.CredentialVault.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;

namespace AutonomiaSaaS.Modules.CredentialVault.Admin.Controllers;

/// <summary>
/// [Admin] é o atributo de rota padrão do Orchard Core para páginas administrativas — coloca
/// isto automaticamente sob /Admin/... e integra com o layout do admin. A checagem de permissão
/// é feita chamada a chamada via IAuthorizationService, não só pelo atributo de rota.
///
/// PRINCÍPIO DO CONTROLLER INTEIRO: nenhuma action aqui jamais devolve um valor de credencial
/// pra view. Isso não é uma convenção a lembrar — é a única coisa que este controller garante.
/// </summary>
[Admin]
public sealed class AgentCredentialsAdminController : Controller
{
    private readonly ICredentialVault _credentialVault;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<AgentCredentialsAdminController> _logger;

    public AgentCredentialsAdminController(
        ICredentialVault credentialVault,
        IAuthorizationService authorizationService,
        ILogger<AgentCredentialsAdminController> logger)
    {
        _credentialVault = credentialVault;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, CredentialVaultPermissions.ManageAgentCredentials))
        {
            return Forbid();
        }

        var summaries = await _credentialVault.ListAsync();

        var model = new AgentCredentialListViewModel
        {
            Credentials = summaries
                .OrderBy(c => c.AgentName).ThenBy(c => c.Key)
                .Select(c => new AgentCredentialSummaryViewModel
                {
                    AgentName = c.AgentName,
                    Key = c.Key,
                    Description = c.Description,
                    CreatedUtc = c.CreatedUtc,
                    RotatedUtc = c.RotatedUtc,
                })
                .ToList(),
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, CredentialVaultPermissions.ManageAgentCredentials))
        {
            return Forbid();
        }

        return View(new AgentCredentialEditViewModel());
    }

    /// <summary>Também serve como tela de "Rotacionar" — mesmo formulário, agentName/key pré-preenchidos, valor sempre em branco.</summary>
    [HttpGet]
    public async Task<IActionResult> Rotate(string agentName, string key)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CredentialVaultPermissions.ManageAgentCredentials))
        {
            return Forbid();
        }

        // Não busca a credencial existente pra pré-popular Value — só existe pra confirmar que
        // a chave já existe e ajustar o texto da tela. O valor em si nunca volta pra UI.
        return View("Create", new AgentCredentialEditViewModel
        {
            AgentName = agentName,
            Key = key,
            IsRotation = true,
        });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePost(AgentCredentialEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CredentialVaultPermissions.ManageAgentCredentials))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _credentialVault.StoreAsync(model.AgentName, model.Key, model.Value, model.Description);

        // Log intencionalmente sem o valor — só metadados e quem fez a ação (User já fica no
        // contexto do request; se o BusinessCore tiver um IClock/auditoria própria, prefira isso
        // no lugar deste log simples).
        _logger.LogInformation(
            "Credencial gravada via admin por {UserName}: {AgentName}/{Key}.",
            User.Identity?.Name, model.AgentName, model.Key);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(string agentName, string key)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CredentialVaultPermissions.ManageAgentCredentials))
        {
            return Forbid();
        }

        await _credentialVault.RevokeAsync(agentName, key);

        _logger.LogInformation(
            "Credencial revogada via admin por {UserName}: {AgentName}/{Key}.",
            User.Identity?.Name, agentName, key);

        return RedirectToAction(nameof(Index));
    }
}
