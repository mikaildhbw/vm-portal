namespace VmPortal.Core.Configuration;

public class LdapSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public string BaseDn { get; set; } = string.Empty;

    /// <summary>
    /// Optionaler Service-Account für Backend-initiierte LDAP-Suchen (z. B. AD-Gruppensuche
    /// fürs Admin-Panel), die nicht im Kontext eines eingeloggten Nutzers laufen -
    /// <see cref="Services.LdapAuthService"/> bindet beim Login weiterhin als der jeweilige
    /// Nutzer selbst und nutzt diese Felder nicht. Bleiben sie leer, versucht
    /// <see cref="Services.LdapAdGroupSearchService"/> einen anonymen Bind, der auf
    /// restriktiv konfigurierten ADs (z. B. Siemens-Produktion) i. d. R. fehlschlägt - dann
    /// müssen hier echte Service-Account-Zugangsdaten hinterlegt werden.
    /// </summary>
    public string ServiceAccountUsername { get; set; } = string.Empty;
    public string ServiceAccountPassword { get; set; } = string.Empty;
}
