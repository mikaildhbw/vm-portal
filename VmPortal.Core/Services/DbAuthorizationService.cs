using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VmPortal.Core.Configuration;
using VmPortal.Core.Data;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Models;

namespace VmPortal.Core.Services;

/// <summary>
/// RBAC-Autorisierung über die SQLite-Schicht: seit der Umstellung von <c>VmController</c>
/// (Commit siehe docs/authorization.md) die einzige tatsächliche Quelle für VM-Autorisierung,
/// nicht mehr nur eine parallele Schicht. Authentifizierung bleibt unverändert Aufgabe von
/// <see cref="LdapAuthService"/> (Hybrid-Architektur: AD authentifiziert, SQLite
/// autorisiert). Details und Begründung der Union-Regel siehe docs/authorization.md.
/// </summary>
public class DbAuthorizationService : IDbAuthorizationService
{
    private readonly VmPortalDbContext _db;
    private readonly AuthorizationSettings _settings;
    private readonly ILogger<DbAuthorizationService> _logger;

    public DbAuthorizationService(VmPortalDbContext db, AuthorizationSettings settings, ILogger<DbAuthorizationService> logger)
    {
        _db = db;
        _settings = settings;
        _logger = logger;
    }

    public bool IsBootstrapFullAdmin(IReadOnlyCollection<string> adGroups) =>
        adGroups.Any(group => string.Equals(group, _settings.BootstrapFullAdminGroup, StringComparison.OrdinalIgnoreCase));

    public async Task<IReadOnlySet<VmAction>> GetAllowedActionsAsync(
        IReadOnlyCollection<string> adGroups, string vmName, string? hostName = null)
    {
        if (IsBootstrapFullAdmin(adGroups))
            return Enum.GetValues<VmAction>().ToHashSet();

        // Ohne hostName (Dummy-Provider, lokaler Hyper-V-Modus mit implizit einem Host) bleibt
        // die Suche wie bisher namensbasiert. Mit hostName wird zusätzlich auf den
        // zugeordneten VirtualServer gefiltert, da VM-Namen im Remote-Multi-Host-Modus nicht
        // host-übergreifend eindeutig sind - siehe XML-Doc auf IDbAuthorizationService.
        var vmQuery = _db.VirtualMachines.AsNoTracking().Where(v => v.Name == vmName);
        if (!string.IsNullOrEmpty(hostName))
            vmQuery = vmQuery.Where(v => v.Server.Name == hostName);

        var vm = await vmQuery.FirstOrDefaultAsync();
        if (vm?.GroupId is not { } groupId)
        {
            // Deckt zwei Fälle ab, die für die Autorisierung gleichbedeutend sind (keine
            // VM-Gruppe zum Prüfen von GroupPermissions vorhanden): die VM ist der
            // Autorisierungs-DB unbekannt, oder sie ist bekannt, aber (noch) keiner
            // VM-Gruppe zugeordnet (secure-by-default).
            _logger.LogWarning(
                "DB-Autorisierung verweigert (VM ohne Gruppe): VM '{VmName}' (Host '{HostName}') ist {VmState}; " +
                "AD-Gruppen des Nutzers: [{AdGroups}]",
                vmName, hostName,
                vm is null ? "der Autorisierungs-DB unbekannt" : "keiner VM-Gruppe zugeordnet",
                string.Join(", ", adGroups));
            return new HashSet<VmAction>();
        }

        var matchingUserGroupIds = await _db.UserGroups
            .AsNoTracking()
            .Where(ug => adGroups.Contains(ug.Name))
            .Select(ug => ug.Id)
            .ToListAsync();

        if (matchingUserGroupIds.Count == 0)
        {
            _logger.LogWarning(
                "DB-Autorisierung verweigert (keine passende GroupPermission): keine der AD-Gruppen [{AdGroups}] " +
                "des Nutzers ist als UserGroup bekannt (VM '{VmName}', VM-Gruppe {GroupId})",
                string.Join(", ", adGroups), vmName, groupId);
            return new HashSet<VmAction>();
        }

        // Union aller RoleActions über ALLE zutreffenden Rollen (nicht "höchste Rolle
        // gewinnt") - Level dient nur der UI-Sortierung, nicht der Rechte-Ermittlung.
        var actionNames = await _db.GroupPermissions
            .AsNoTracking()
            .Where(gp => gp.VmGroupId == groupId && matchingUserGroupIds.Contains(gp.UserGroupId))
            .SelectMany(gp => gp.Role.RoleActions.Select(ra => ra.Action.Name))
            .Distinct()
            .ToListAsync();

        if (actionNames.Count == 0)
        {
            _logger.LogWarning(
                "DB-Autorisierung verweigert (keine passende GroupPermission): AD-Gruppen [{AdGroups}] sind zwar " +
                "bekannt, haben aber keine GroupPermission auf VM-Gruppe {GroupId} (VM '{VmName}')",
                string.Join(", ", adGroups), groupId, vmName);
        }

        return actionNames
            .Where(name => Enum.TryParse<VmAction>(name, out _))
            .Select(name => Enum.Parse<VmAction>(name))
            .ToHashSet();
    }

    public async Task<bool> IsAllowedAsync(
        IReadOnlyCollection<string> adGroups, string vmName, VmAction action, string? hostName = null)
    {
        var allowedActions = await GetAllowedActionsAsync(adGroups, vmName, hostName);
        return allowedActions.Contains(action);
    }

    public async Task<IReadOnlyList<VmReference>> GetAuthorizedVmsAsync(
        IReadOnlyCollection<string> adGroups, VmAction action)
    {
        // Eine einzige Abfrage statt einer Prüfung pro VM (vorher: N VMs * 3 Queries) -
        // adgroups-Claim -> UserGroups -> GroupPermissions (mit passender RoleAction) ->
        // VirtualMachines, direkt als Join/Exists von EF Core in EIN SQL-Statement übersetzt.
        // Bootstrap-FullAdmin wird hier bewusst NICHT behandelt (siehe XML-Doc auf dem
        // Interface) - der hat keine GroupPermission-Zeile, gegen die sich filtern ließe.
        var actionName = action.ToString();

        var rows = await
            (from vm in _db.VirtualMachines.AsNoTracking()
             where vm.GroupId != null
             join gp in _db.GroupPermissions.AsNoTracking() on vm.GroupId equals gp.VmGroupId
             join ug in _db.UserGroups.AsNoTracking() on gp.UserGroupId equals ug.Id
             where adGroups.Contains(ug.Name)
             where gp.Role.RoleActions.Any(ra => ra.Action.Name == actionName)
             select new { vm.Name, ServerName = vm.Server.Name })
            .Distinct()
            .ToListAsync();

        return rows.Select(r => new VmReference(r.ServerName, r.Name)).ToList();
    }
}
