using VmPortal.Core.Models;

namespace VmPortal.Core.Interfaces;

public interface IVirtualizationProvider
{
    Task<IEnumerable<VirtualMachine>> GetVmsAsync();
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
