namespace VmPortal.Core.Data.Entities;

/// <summary>
/// Entspricht 1:1 einer AD-Gruppe - kein Sync, nur Referenz per Name (Groupname aus
/// dem "memberOf"-Attribut des Nutzers im AD).
/// </summary>
public class UserGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<GroupPermission> GroupPermissions { get; set; } = new();
}
