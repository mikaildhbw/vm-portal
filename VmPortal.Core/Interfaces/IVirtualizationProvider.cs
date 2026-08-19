using VmPortal.Core.Models;

namespace VmPortal.Core.Interfaces;

public interface IVirtualizationProvider
{
    /// <summary>Volles Inventar aller konfigurierten Hosts, ungefiltert. Teuer im
    /// Remote-Multi-Host-Modus (ein Get-VM ohne Filter pro Host) - nur für Kontexte gedacht,
    /// die tatsächlich alles brauchen (z. B. Bootstrap-FullAdmin-Sicht, Admin-Werkzeuge),
    /// nicht für die reguläre, autorisierungsgefilterte VM-Liste. Siehe
    /// <see cref="GetVmsAsync(IReadOnlyCollection{VmReference})"/> für den gezielten Abruf.</summary>
    Task<IEnumerable<VirtualMachine>> GetVmsAsync();

    /// <summary>Fragt gezielt nur die übergebenen VMs ab (pro Host per Namensliste in einem
    /// Aufruf statt des kompletten Inventars) - für die autorisierungsgefilterte VM-Liste:
    /// erst per DB ermitteln, was der Nutzer sehen darf (<see cref="Interfaces.IDbAuthorizationService.GetAuthorizedVmsAsync"/>),
    /// dann nur dafür beim Hypervisor nachfragen. Hosts ohne Einträge in
    /// <paramref name="authorizedVms"/> werden gar nicht angefragt.</summary>
    Task<IEnumerable<VirtualMachine>> GetVmsAsync(IReadOnlyCollection<VmReference> authorizedVms);

    Task<VirtualMachine?> GetVmByIdAsync(string id);
    Task StartVmAsync(string id);
    Task StopVmAsync(string id);
    Task ResetVmAsync(string id);
    Task CreateSnapshotAsync(string id, string snapshotName);

    Task<VmMeteringData?> GetMeteringAsync(string id);
    Task PauseVmAsync(string id);
    Task ResumeVmAsync(string id);
    Task SaveStateAsync(string id);
    Task ApplySnapshotAsync(string id, string snapshotName);
    Task DeleteSnapshotAsync(string id, string snapshotName);
    Task<string> GetConsoleConnectionAsync(string id);
    Task ResizeRamAsync(string id, int ramMb);
    Task ResizeCpuAsync(string id, int cpuCount);
    Task AttachNetworkAdapterAsync(string id, string switchName);
    Task ResizeVhdAsync(string id, int sizeGb);
    Task CompactVhdAsync(string id);
    Task ExportVmAsync(string id, string exportPath);
    Task ImportVmAsync(string importPath);
    Task CloneVmAsync(string id, string newName);
    Task LiveMigrateVmAsync(string id, string targetHost);
}
