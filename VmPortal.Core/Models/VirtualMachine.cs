namespace VmPortal.Core.Models;

public class VirtualMachine
{
    /// <summary>
    /// Eindeutiger Bezeichner für Provider-Aufrufe (GetVmByIdAsync, StartVmAsync, ...).
    /// Im lokalen Hyper-V-Modus weiterhin der VM-Name (wie bisher). Im Remote-Multi-Host-Modus
    /// des <see cref="Services.HyperVProvider"/> die Kombination "Hostname::VM-GUID", da
    /// VM-Namen über mehrere Hosts hinweg nicht eindeutig sind - siehe <see cref="HostName"/>.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public VmStatus Status { get; set; }
    public string AssignedUserId { get; set; } = string.Empty;

    /// <summary>
    /// Name des Hyper-V-Hosts, auf dem die VM läuft (Server-Name aus
    /// Virtualization:HyperV:Hosts). Leer im lokalen Modus und im Dummy-Provider, da dort nur
    /// ein impliziter Host existiert.
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// Die von Get-VM gelieferte Hyper-V-VM-GUID (Attribut "Id"), als String. Zusammen mit
    /// <see cref="HostName"/> die robuste, host-eindeutige Identität einer VM - der VM-Name
    /// allein kollidiert nachweislich zwischen Hosts (siehe docs/PROJEKT_ERKLAERUNG.md).
    /// </summary>
    public string VmGuid { get; set; } = string.Empty;
}

public enum VmStatus
{
    Running,
    Stopped,
    Paused,
    Unknown
}
