namespace VmPortal.Core.Models;

/// <summary>
/// Hierarchisches Rollenmodell für VM-Berechtigungen. Die Enum-Werte sind aufsteigend
/// gewählt, sodass ein einfacher Zahlenvergleich die Vererbung abbildet:
/// eine höhere Rolle erbt alle Rechte der niedrigeren Rollen.
/// </summary>
public enum VmRole
{
    Viewer = 0,
    Operator = 1,
    PowerUser = 2,
    Admin = 3,
    FullAdmin = 4
}
