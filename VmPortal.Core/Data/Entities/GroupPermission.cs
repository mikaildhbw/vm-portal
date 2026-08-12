namespace VmPortal.Core.Data.Entities;

/// <summary>
/// Weist einer AD-Gruppe (<see cref="UserGroup"/>) eine Rolle auf einer VM-Gruppe
/// (<see cref="VirtualMachineGroup"/>) zu. Ein UserGroup/VmGroup-Paar kann mehrere
/// GroupPermission-Zeilen (und damit mehrere Rollen gleichzeitig) haben - z. B. wenn eine
/// AD-Gruppe zwei verschiedene Custom-Rollen auf dieselbe VM-Gruppe zugewiesen bekommt.
/// Der eindeutige Schlüssel läuft daher über alle drei Spalten (VmGroupId, UserGroupId,
/// RoleId), siehe <see cref="VmPortalDbContext"/>. Das ist eine bewusste Design-Entscheidung
/// (nicht UNIQUE(VmGroupId, UserGroupId)): sie erlaubt Mehrfachauswahl von Rollen pro
/// Zuordnung in der Admin-UI und passt zur Union-Autorisierungslogik in
/// DbAuthorizationService (Rechte mehrerer zutreffender Rollen werden vereinigt, nicht
/// nur die "höchste" Rolle gilt).
/// </summary>
public class GroupPermission
{
    public int Id { get; set; }

    public int VmGroupId { get; set; }
    public VirtualMachineGroup VmGroup { get; set; } = null!;

    public int UserGroupId { get; set; }
    public UserGroup UserGroup { get; set; } = null!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
