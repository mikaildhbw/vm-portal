namespace VmPortal.Core.Configuration;

/// <summary>
/// Simulierte Nutzer->VM->Rolle-Zuordnungen für die lokale Entwicklung ohne echtes AD.
/// Wird aus dem Abschnitt "TestVmRoles" gebunden, z. B.:
/// <code>
/// "TestVmRoles": {
///   "Users": {
///     "mugur": { "VM-Mikail": "PowerUser" }
///   }
/// }
/// </code>
/// </summary>
public class TestVmRolesSettings
{
    public Dictionary<string, Dictionary<string, string>> Users { get; set; } = new();
}
