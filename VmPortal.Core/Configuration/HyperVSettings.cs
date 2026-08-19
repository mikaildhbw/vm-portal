namespace VmPortal.Core.Configuration;

/// <summary>
/// Ausführungsmodus des <see cref="Services.HyperVProvider"/>: <see cref="Local"/> führt die
/// Hyper-V-Cmdlets in-process aus (App läuft auf dem Hyper-V-Host selbst, bisheriges
/// Verhalten). <see cref="Remote"/> steuert einen oder mehrere Hyper-V-Hosts per
/// WinRM/Kerberos an (App läuft auf einem separaten Server).
/// </summary>
public enum HyperVMode
{
    Local,
    Remote
}

/// <summary>
/// Bindet den Konfigurationsabschnitt "Virtualization:HyperV". Ohne diesen Abschnitt (z. B.
/// bestehende Testumgebungs-Konfiguration) bleibt <see cref="Mode"/> auf <see cref="HyperVMode.Local"/>
/// - identisch zum bisherigen, ausschließlich lokalen Verhalten.
/// </summary>
public class HyperVSettings
{
    public HyperVMode Mode { get; set; } = HyperVMode.Local;
    public List<HyperVHostSettings> Hosts { get; set; } = new();
    public HyperVRemoteSettings Remote { get; set; } = new();
}

public class HyperVHostSettings
{
    public string Name { get; set; } = string.Empty;
    public string FQDN { get; set; } = string.Empty;
}

/// <summary>
/// WinRM-Verbindungsparameter, gelten für alle konfigurierten Hosts gemeinsam. Die
/// Authentifizierung erfolgt über die Prozessidentität des ausführenden Kontos (kein
/// Credential in der Konfiguration) - passend zur verifizierten Kerberos-Konnektivität
/// gegen alle drei Ziel-Hosts.
/// </summary>
public class HyperVRemoteSettings
{
    public int Port { get; set; } = 5985;
    public bool UseSsl { get; set; } = false;
    public string Authentication { get; set; } = "Kerberos";
}
