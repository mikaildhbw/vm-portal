using Microsoft.EntityFrameworkCore;
using VmPortal.Core.Configuration;
using VmPortal.Core.Data;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Models;

namespace VmPortal.Core.Services;

/// <summary>
/// RBAC-Autorisierung über die SQLite-Schicht: löst die bisherige TestVmRoles-Simulation
/// als Autorisierungsquelle ab. Authentifizierung bleibt unverändert Aufgabe von
/// <see cref="LdapAuthService"/> (Hybrid-Architektur: AD authentifiziert, SQLite
/// autorisiert). Details und Begründung der Union-Regel siehe docs/authorization.md.
/// </summary>
public class DbAuthorizationService : IDbAuthorizationService
{
    private readonly VmPortalDbContext _db;
    private readonly AuthorizationSettings _settings;

    public DbAuthorizationService(VmPortalDbContext db, AuthorizationSettings settings)
    {
        _db = db;
        _settings = settings;
    }

    public bool IsBootstrapFullAdmin(IReadOnlyCollection<string> adGroups) =>
        adGroups.Any(group => string.Equals(group, _settings.BootstrapFullAdminGroup, StringComparison.OrdinalIgnoreCase));

    public async Task<IReadOnlySet<VmAction>> GetAllowedActionsAsync(IReadOnlyCollection<string> adGroups, string vmName)
    {
        if (IsBootstrapFullAdmin(adGroups))
            return Enum.GetValues<VmAction>().ToHashSet();

        var vm = await _db.VirtualMachines.AsNoTracking().FirstOrDefaultAsync(v => v.Name == vmName);
        if (vm?.GroupId is not { } groupId)
            return new HashSet<VmAction>();

        var matchingUserGroupIds = await _db.UserGroups
            .AsNoTracking()
            .Where(ug => adGroups.Contains(ug.Name))
            .Select(ug => ug.Id)
            .ToListAsync();

        if (matchingUserGroupIds.Count == 0)
            return new HashSet<VmAction>();

        // Union aller RoleActions über ALLE zutreffenden Rollen (nicht "höchste Rolle
        // gewinnt") - Level dient nur der UI-Sortierung, nicht der Rechte-Ermittlung.
        var actionNames = await _db.GroupPermissions
            .AsNoTracking()
            .Where(gp => gp.VmGroupId == groupId && matchingUserGroupIds.Contains(gp.UserGroupId))
            .SelectMany(gp => gp.Role.RoleActions.Select(ra => ra.Action.Name))
            .Distinct()
            .ToListAsync();

        return actionNames
            .Where(name => Enum.TryParse<VmAction>(name, out _))
            .Select(name => Enum.Parse<VmAction>(name))
            .ToHashSet();
    }

    public async Task<bool> IsAllowedAsync(IReadOnlyCollection<string> adGroups, string vmName, VmAction action)
    {
        var allowedActions = await GetAllowedActionsAsync(adGroups, vmName);
        return allowedActions.Contains(action);
    }
}
