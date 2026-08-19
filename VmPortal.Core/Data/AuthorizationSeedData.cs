using VmPortal.Core.Data.Entities;
using VmPortal.Core.Models;
using VmPortal.Core.Services;

namespace VmPortal.Core.Data;

/// <summary>
/// Seed-Daten für die Autorisierungsschicht, gebacken in die Migration
/// "InitialAuthorizationSchema" (siehe <see cref="VmPortalDbContext.OnModelCreating"/>).
/// Die RoleActions der fünf System-Rollen werden aus der bestehenden
/// <see cref="RolePermissions"/>-Klasse übernommen statt neu erfunden, damit der bisherige
/// Berechtigungsstand 1:1 als Ausgangspunkt der neuen, nicht mehr automatisch vererbenden
/// RBAC-Matrix übernommen wird.
/// </summary>
public static class AuthorizationSeedData
{
    // IDs = Enum-Wert + 1 (Enums beginnen bei 0, PKs konventionell bei 1).
    public static int RoleId(VmRole role) => (int)role + 1;
    public static int ActionId(VmAction action) => (int)action + 1;

    public static IReadOnlyList<Role> Roles { get; } = Enum.GetValues<VmRole>()
        .Select(role => new Role
        {
            Id = RoleId(role),
            Name = role.ToString(),
            IsSystemRole = true,
            Level = (int)role
        })
        .ToList();

    public static IReadOnlyList<VmActionEntity> VMActions { get; } = Enum.GetValues<VmAction>()
        .Select(action => new VmActionEntity { Id = ActionId(action), Name = action.ToString() })
        .ToList();

    public static IReadOnlyList<RoleAction> RoleActions { get; } =
        (from role in Enum.GetValues<VmRole>()
         from action in Enum.GetValues<VmAction>()
         where RolePermissions.IsAllowed(role, action)
         select new RoleAction { RoleId = RoleId(role), ActionId = ActionId(action) })
        .ToList();

    // Verifiziert 2026-08-19: genau drei eigenständige Hyper-V-Hosts. Ein vierter Eintrag
    // "MHM-VCLUSTER1" existierte hier ursprünglich, bezeichnet aber keinen eigenen Host -
    // die zugehörige IP ist eine zweite NIC von MHM-HYPERV4 (siehe docs/PROJEKT_ERKLAERUNG.md).
    public static IReadOnlyList<VirtualServer> VirtualServers { get; } = new List<VirtualServer>
    {
        new() { Id = 1, Address = "MHM-HYPERV1.archiv.mhm.siemens.com", Platform = "HyperV", Name = "MHM-HYPERV1" },
        new() { Id = 2, Address = "MHM-HYPERV3.archiv.mhm.siemens.com", Platform = "HyperV", Name = "MHM-HYPERV3" },
        new() { Id = 3, Address = "MHM-HYPERV4.archiv.mhm.siemens.com", Platform = "HyperV", Name = "MHM-HYPERV4" }
    };

    // Beide Bootstrap-FullAdmin-Gruppennamen (lokale Testumgebung und Siemens-AD) werden
    // geseedet, damit Authorization:BootstrapFullAdminGroup in beiden appsettings-Varianten
    // auf eine existierende UserGroup verweist.
    public static IReadOnlyList<UserGroup> UserGroups { get; } = new List<UserGroup>
    {
        new() { Id = 1, Name = "ESX Admins" },
        new() { Id = 2, Name = "VM-Portal-Benutzer" }
    };

    // --- Testberechtigung für den Verfasser-Account (Migration "SeedTestUserPermissions") ---
    // Reine Testdaten, keine Erweiterung der übrigen Berechtigungsmatrix: die AD-Gruppe
    // "ESXUserIT" (Verfasser, kein FullAdmin) bekommt Rolle PowerUser auf genau den neun
    // Hyper-V-Test-VMs HVP_1-HVP_9 auf MHM-HYPERV4. Es gibt aktuell weder einen
    // VM-Discovery-/Sync-Mechanismus noch eine Admin-UI-Funktion, um einzelne VMs einer
    // VirtualMachineGroup zuzuordnen (VmGroupsController verwaltet nur Gruppennamen, nicht
    // die Mitgliedschaft) - die VirtualMachines-Tabelle wird daher bislang ausschließlich per
    // Seed/Migration befüllt, siehe Bericht zu dieser Aufgabe.
    private const int TestUserGroupId = 3;
    private const int TestVmGroupId = 1;
    private const int TestHyperV4ServerId = 3; // MHM-HYPERV4, siehe VirtualServers oben

    public static IReadOnlyList<UserGroup> TestUserGroups { get; } = new List<UserGroup>
    {
        new() { Id = TestUserGroupId, Name = "ESXUserIT" }
    };

    public static IReadOnlyList<VirtualMachineGroup> TestVirtualMachineGroups { get; } = new List<VirtualMachineGroup>
    {
        new() { Id = TestVmGroupId, Name = "Testumgebung-HVP" }
    };

    public static IReadOnlyList<VirtualMachineRecord> TestVirtualMachines { get; } = Enumerable.Range(1, 9)
        .Select(i => new VirtualMachineRecord
        {
            Id = i,
            Name = $"HVP_{i}",
            ServerId = TestHyperV4ServerId,
            GroupId = TestVmGroupId
        })
        .ToList();

    public static IReadOnlyList<GroupPermission> TestGroupPermissions { get; } = new List<GroupPermission>
    {
        new() { Id = 1, VmGroupId = TestVmGroupId, UserGroupId = TestUserGroupId, RoleId = RoleId(VmRole.PowerUser) }
    };
}
