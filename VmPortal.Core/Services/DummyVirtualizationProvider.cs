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

    public Task<IEnumerable<VirtualMachine>> GetVmsAsync(IReadOnlyCollection<VmReference> authorizedVms)
    {
        var names = authorizedVms.Select(v => v.Name).ToHashSet();
        return Task.FromResult(_vms.Where(vm => names.Contains(vm.Name)).AsEnumerable());
    }

    public Task<VirtualMachine?> GetVmByIdAsync(string id) =>
        Task.FromResult(_vms.FirstOrDefault(v => v.Id == id));

    public Task StartVmAsync(string id) { Console.WriteLine($"[Dummy] Start VM {id}"); return Task.CompletedTask; }
    public Task StopVmAsync(string id) { Console.WriteLine($"[Dummy] Stop VM {id}"); return Task.CompletedTask; }
    public Task ResetVmAsync(string id) { Console.WriteLine($"[Dummy] Reset VM {id}"); return Task.CompletedTask; }
    public Task CreateSnapshotAsync(string id, string snapshotName) { Console.WriteLine($"[Dummy] Snapshot {snapshotName} für VM {id}"); return Task.CompletedTask; }

    public Task<VmMeteringData?> GetMeteringAsync(string id)
    {
        Console.WriteLine($"[Dummy] Metering für VM {id}");
        return Task.FromResult<VmMeteringData?>(new VmMeteringData("120 MHz", "512 MB", "2048 MB"));
    }

    public Task PauseVmAsync(string id) { Console.WriteLine($"[Dummy] Pause VM {id}"); return Task.CompletedTask; }
    public Task ResumeVmAsync(string id) { Console.WriteLine($"[Dummy] Resume VM {id}"); return Task.CompletedTask; }
    public Task SaveStateAsync(string id) { Console.WriteLine($"[Dummy] SaveState VM {id}"); return Task.CompletedTask; }
    public Task ApplySnapshotAsync(string id, string snapshotName) { Console.WriteLine($"[Dummy] Snapshot {snapshotName} anwenden auf VM {id}"); return Task.CompletedTask; }
    public Task DeleteSnapshotAsync(string id, string snapshotName) { Console.WriteLine($"[Dummy] Snapshot {snapshotName} löschen von VM {id}"); return Task.CompletedTask; }

    public Task<string> GetConsoleConnectionAsync(string id)
    {
        Console.WriteLine($"[Dummy] Konsolenverbindung für VM {id}");
        return Task.FromResult($"dummy-console://{id}");
    }

    public Task ResizeRamAsync(string id, int ramMb) { Console.WriteLine($"[Dummy] RAM von VM {id} auf {ramMb} MB"); return Task.CompletedTask; }
    public Task ResizeCpuAsync(string id, int cpuCount) { Console.WriteLine($"[Dummy] CPUs von VM {id} auf {cpuCount}"); return Task.CompletedTask; }
    public Task AttachNetworkAdapterAsync(string id, string switchName) { Console.WriteLine($"[Dummy] Netzwerkadapter an Switch {switchName} für VM {id}"); return Task.CompletedTask; }
    public Task ResizeVhdAsync(string id, int sizeGb) { Console.WriteLine($"[Dummy] VHD von VM {id} auf {sizeGb} GB"); return Task.CompletedTask; }
    public Task CompactVhdAsync(string id) { Console.WriteLine($"[Dummy] VHD von VM {id} kompaktieren"); return Task.CompletedTask; }
    public Task ExportVmAsync(string id, string exportPath) { Console.WriteLine($"[Dummy] Export VM {id} nach {exportPath}"); return Task.CompletedTask; }
    public Task ImportVmAsync(string importPath) { Console.WriteLine($"[Dummy] Import VM aus {importPath}"); return Task.CompletedTask; }
    public Task CloneVmAsync(string id, string newName) { Console.WriteLine($"[Dummy] Clone VM {id} als {newName}"); return Task.CompletedTask; }
    public Task LiveMigrateVmAsync(string id, string targetHost) { Console.WriteLine($"[Dummy] LiveMigrate VM {id} nach {targetHost}"); return Task.CompletedTask; }
}
