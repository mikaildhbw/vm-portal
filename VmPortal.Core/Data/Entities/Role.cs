namespace VmPortal.Core.Data.Entities;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>True für die fünf Basis-Rollen (Viewer, Operator, PowerUser, Admin, FullAdmin) -
    /// nicht löschbar, nicht umbenennbar, ihre RoleActions nicht editierbar.</summary>
    public bool IsSystemRole { get; set; }

    /// <summary>Nur für Sortierung/Anzeige in der Admin-UI - keine automatische
    /// Rechte-Vererbung mehr darüber, siehe docs/authorization.md.</summary>
    public int Level { get; set; }

    public List<RoleAction> RoleActions { get; set; } = new();
    public List<GroupPermission> GroupPermissions { get; set; } = new();
}
