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

    public static IReadOnlyList<VirtualServer> VirtualServers { get; } = new List<VirtualServer>
    {
        new() { Id = 1, Address = "MHM-HYPERV1.archiv.mhm.siemens.com", Platform = "HyperV", Name = "MHM-HYPERV1" },
        new() { Id = 2, Address = "MHM-HYPERV3.archiv.mhm.siemens.com", Platform = "HyperV", Name = "MHM-HYPERV3" },
        new() { Id = 3, Address = "MHM-HYPERV4.archiv.mhm.siemens.com", Platform = "HyperV", Name = "MHM-HYPERV4" },
        new() { Id = 4, Address = "MHM-VCLUSTER1.archiv.mhm.siemens.com", Platform = "HyperV", Name = "MHM-VCLUSTER1" }
    };

    // Beide Bootstrap-FullAdmin-Gruppennamen (lokale Testumgebung und Siemens-AD) werden
    // geseedet, damit Authorization:BootstrapFullAdminGroup in beiden appsettings-Varianten
    // auf eine existierende UserGroup verweist.
    public static IReadOnlyList<UserGroup> UserGroups { get; } = new List<UserGroup>
    {
        new() { Id = 1, Name = "ESX Admins" },
        new() { Id = 2, Name = "VM-Portal-Benutzer" }
    };
}
