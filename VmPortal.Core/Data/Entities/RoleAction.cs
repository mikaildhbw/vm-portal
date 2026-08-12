namespace VmPortal.Core.Data.Entities;

/// <summary>
/// Definiert EXPLIZIT und VOLLSTÄNDIG, was eine Rolle darf - keine implizite Vererbung
/// von anderen Rollen (siehe docs/authorization.md).
/// </summary>
public class RoleAction
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public int ActionId { get; set; }
    public VmActionEntity Action { get; set; } = null!;
}
