using VmPortal.Core.Models;

namespace VmPortal.Core.Services;

/// <summary>
/// Statische Zuordnung von VM-Aktionen zur jeweils mindestens benötigten Rolle.
/// Die Rollenvererbung ergibt sich aus dem Zahlenvergleich der <see cref="VmRole"/>-Werte:
/// eine höhere Rolle darf alles, was eine niedrigere darf.
/// </summary>
public static class RolePermissions
{
    private static readonly Dictionary<VmAction, VmRole> MinimumRoles = new()
    {
        [VmAction.ViewStatus] = VmRole.Viewer,
        [VmAction.ViewDetails] = VmRole.Viewer,
        [VmAction.ViewMetering] = VmRole.Viewer,

        [VmAction.Start] = VmRole.Operator,
        [VmAction.Stop] = VmRole.Operator,
        [VmAction.Pause] = VmRole.Operator,
        [VmAction.Resume] = VmRole.Operator,
        [VmAction.SaveState] = VmRole.Operator,

        [VmAction.Reset] = VmRole.PowerUser,
        [VmAction.SnapshotCreate] = VmRole.PowerUser,
        [VmAction.SnapshotApply] = VmRole.PowerUser,
        [VmAction.ConsoleConnect] = VmRole.PowerUser,

        [VmAction.SnapshotDelete] = VmRole.Admin,
        [VmAction.ResizeRam] = VmRole.Admin,
        [VmAction.ResizeCpu] = VmRole.Admin,
        [VmAction.AttachNetworkAdapter] = VmRole.Admin,
        [VmAction.VhdResize] = VmRole.Admin,
        [VmAction.VhdCompact] = VmRole.Admin,

        [VmAction.Export] = VmRole.FullAdmin,
        [VmAction.Import] = VmRole.FullAdmin,
        [VmAction.Clone] = VmRole.FullAdmin,
        [VmAction.LiveMigrate] = VmRole.FullAdmin
    };

    public static VmRole MinimumRoleFor(VmAction action)
    {
        if (!MinimumRoles.TryGetValue(action, out var minimumRole))
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unbekannte VM-Aktion");

        return minimumRole;
    }

    public static bool IsAllowed(VmRole userRole, VmAction action) =>
        userRole >= MinimumRoleFor(action);
}
