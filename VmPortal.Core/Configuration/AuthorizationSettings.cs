namespace VmPortal.Core.Configuration;

/// <summary>
/// Name der AD-Gruppe, deren Mitglieder den Bootstrap-FullAdmin-Zugriff erhalten (alle
/// Aktionen auf allen VMs, ohne GroupPermissions-Eintrag). Umgebungsabhängig:
/// "VM-Portal-Benutzer" in der lokalen Testumgebung, "ESX Admins" im Siemens-AD.
/// </summary>
public class AuthorizationSettings
{
    public string BootstrapFullAdminGroup { get; set; } = string.Empty;
}
