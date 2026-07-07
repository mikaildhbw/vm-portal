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
}
