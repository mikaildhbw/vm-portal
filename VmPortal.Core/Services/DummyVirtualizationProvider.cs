using VmPortal.Core.Interfaces;
using VmPortal.Core.Models;

namespace VmPortal.Core.Services;

public class DummyVirtualizationProvider : IVirtualizationProvider
{
    private readonly List<VirtualMachine> _vms = new()
    {
        new VirtualMachine { Id = "vm-001", Name = "VM-Mikail", Status = VmStatus.Stopped, AssignedUserId = "mugur" },
        new VirtualMachine { Id = "vm-002", Name = "VM-Burath", Status = VmStatus.Running, AssignedUserId = "jburath" },
    };

    public Task<IEnumerable<VirtualMachine>> GetVmsAsync() =>
        Task.FromResult(_vms.AsEnumerable());

    public Task<VirtualMachine?> GetVmByIdAsync(string id) =>
        Task.FromResult(_vms.FirstOrDefault(v => v.Id == id));

    public Task StartVmAsync(string id) { Console.WriteLine($"[Dummy] Start VM {id}"); return Task.CompletedTask; }
    public Task StopVmAsync(string id) { Console.WriteLine($"[Dummy] Stop VM {id}"); return Task.CompletedTask; }
    public Task ResetVmAsync(string id) { Console.WriteLine($"[Dummy] Reset VM {id}"); return Task.CompletedTask; }
    public Task CreateSnapshotAsync(string id, string snapshotName) { Console.WriteLine($"[Dummy] Snapshot {snapshotName} für VM {id}"); return Task.CompletedTask; }
}
