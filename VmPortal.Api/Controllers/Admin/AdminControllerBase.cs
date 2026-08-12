using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Services;

namespace VmPortal.Api.Controllers.Admin;

/// <summary>
/// Basis für die Admin-Endpunkte aus TEIL 5: erfordert ein gültiges JWT ([Authorize],
/// bestehendes Cookie-Auth-Pattern) UND zusätzlich globalen FullAdmin-Zugriff, d. h.
/// Mitgliedschaft in der konfigurierten Bootstrap-Gruppe (Authorization:BootstrapFullAdminGroup).
/// Rollenverwaltung ist ein globales, nicht VM-Gruppen-gebundenes Anliegen - daher genügt
/// hier die Bootstrap-Prüfung aus DbAuthorizationService, ohne GroupPermissions abzufragen.
/// </summary>
[ApiController]
[Authorize]
public abstract class AdminControllerBase : ControllerBase, IActionFilter
{
    protected readonly IDbAuthorizationService AuthorizationService;

    protected AdminControllerBase(IDbAuthorizationService authorizationService)
    {
        AuthorizationService = authorizationService;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var adGroups = AdGroupClaims.FromPrincipal(context.HttpContext.User);
        if (!AuthorizationService.IsBootstrapFullAdmin(adGroups))
            context.Result = new ForbidResult();
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
