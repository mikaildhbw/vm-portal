namespace VmPortal.Core.Configuration;

/// <summary>
/// Simulierte AD-Gruppenmitgliedschaften für die lokale Entwicklung ohne echtes AD -
/// Pendant zu <see cref="TestVmRolesSettings"/>, aber für den "adgroups"-Claim
/// (DbAuthorizationService), z. B.:
/// <code>
/// "TestAdGroups": {
///   "Users": {
///     "mugur": [ "VM-Portal-Benutzer" ]
///   }
/// }
/// </code>
/// </summary>
public class TestAdGroupsSettings
{
    public Dictionary<string, List<string>> Users { get; set; } = new();
}
