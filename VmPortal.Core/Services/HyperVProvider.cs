using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.Extensions.Logging;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Models;

namespace VmPortal.Core.Services;

/// <summary>
/// Konkrete Umsetzung von <see cref="IVirtualizationProvider"/> für Microsoft Hyper-V.
/// Die App läuft direkt auf dem Hyper-V-Host, daher werden die Hyper-V-Cmdlets über eine
/// lokale PowerShell-Instanz ausgeführt — ohne WinRM/Remoting und ohne Netzwerkverbindung.
/// Die Abstraktion über das Interface erlaubt es, denselben Portal-Kern gegen andere
/// Hypervisoren (z. B. Proxmox) zu betreiben, ohne die API-Schicht zu ändern — das ist der
/// plattformunabhängige Kern der Arbeit.
/// </summary>
public class HyperVProvider : IVirtualizationProvider
{
    private const long BytesPerMegabyte = 1024L * 1024L;
    private const long BytesPerGigabyte = 1024L * 1024L * 1024L;

    private readonly ILogger<HyperVProvider> _logger;

    public HyperVProvider(ILogger<HyperVProvider> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<VirtualMachine>> GetVmsAsync()
    {
        _logger.LogInformation("Rufe alle VMs vom lokalen Hyper-V-Host ab");
        var results = await InvokeAsync(ps => ps.AddCommand("Get-VM"));
        return results.Select(MapVm).ToList();
    }

    public async Task<VirtualMachine?> GetVmByIdAsync(string id)
    {
        _logger.LogInformation("Rufe VM {VmId} vom lokalen Hyper-V-Host ab", id);
        var results = await InvokeAsync(ps => ps
            .AddCommand("Get-VM")
            .AddParameter("Name", id)
            .AddParameter("ErrorAction", "SilentlyContinue"));

        var vm = results.FirstOrDefault();
        return vm is null ? null : MapVm(vm);
    }

    public async Task StartVmAsync(string id)
    {
        _logger.LogInformation("Starte VM {VmId}", id);
        await InvokeAsync(ps => ps.AddCommand("Start-VM").AddParameter("Name", id));
    }

    public async Task StopVmAsync(string id)
    {
        _logger.LogInformation("Stoppe VM {VmId}", id);
        await InvokeAsync(ps => ps
            .AddCommand("Stop-VM")
            .AddParameter("Name", id)
            .AddParameter("Force", true));
    }

    public async Task ResetVmAsync(string id)
    {
        _logger.LogInformation("Starte VM {VmId} neu", id);
        await InvokeAsync(ps => ps
            .AddCommand("Restart-VM")
            .AddParameter("Name", id)
            .AddParameter("Force", true));
    }

    public async Task CreateSnapshotAsync(string id, string snapshotName)
    {
        _logger.LogInformation("Erstelle Snapshot {SnapshotName} für VM {VmId}", snapshotName, id);
        await InvokeAsync(ps => ps
            .AddCommand("Checkpoint-VM")
            .AddParameter("Name", id)
            .AddParameter("SnapshotName", snapshotName));
    }

    public async Task<VmMeteringData?> GetMeteringAsync(string id)
    {
        _logger.LogInformation("Rufe Metering-Daten für VM {VmId} ab", id);
        var results = await InvokeAsync(ps => ps
            .AddCommand("Measure-VM")
            .AddParameter("VMName", id));

        var report = results.FirstOrDefault();
        return report is null
            ? null
            : new VmMeteringData(
                GetProperty(report, "AvgCPU"),
                GetProperty(report, "AvgRAM"),
                GetProperty(report, "TotalDisk"));
    }

    public async Task PauseVmAsync(string id)
    {
        _logger.LogInformation("Pausiere VM {VmId}", id);
        await InvokeAsync(ps => ps.AddCommand("Suspend-VM").AddParameter("Name", id));
    }

    public async Task ResumeVmAsync(string id)
    {
        _logger.LogInformation("Setze VM {VmId} fort", id);
        await InvokeAsync(ps => ps.AddCommand("Resume-VM").AddParameter("Name", id));
    }

    public async Task SaveStateAsync(string id)
    {
        _logger.LogInformation("Speichere Zustand von VM {VmId}", id);
        await InvokeAsync(ps => ps.AddCommand("Save-VM").AddParameter("Name", id));
    }

    public async Task ApplySnapshotAsync(string id, string snapshotName)
    {
        _logger.LogInformation("Wende Snapshot {SnapshotName} auf VM {VmId} an", snapshotName, id);
        await InvokeAsync(ps => ps
            .AddCommand("Restore-VMSnapshot")
            .AddParameter("VMName", id)
            .AddParameter("Name", snapshotName)
            .AddParameter("Confirm", false));
    }

    public async Task DeleteSnapshotAsync(string id, string snapshotName)
    {
        _logger.LogInformation("Lösche Snapshot {SnapshotName} von VM {VmId}", snapshotName, id);
        await InvokeAsync(ps => ps
            .AddCommand("Remove-VMSnapshot")
            .AddParameter("VMName", id)
            .AddParameter("Name", snapshotName)
            .AddParameter("Confirm", false));
    }

    public Task<string> GetConsoleConnectionAsync(string id) =>
        // Es gibt kein Hyper-V-Cmdlet für Konsolenzugriff: vmconnect.exe ist eine
        // GUI-Anwendung und liefert keinen Stream, den ein Web-Portal durchreichen
        // könnte. Eine Web-Konsole erfordert zusätzliche Infrastruktur
        // (z. B. RDP-/WebSocket-Gateway) und liegt außerhalb des aktuellen Scopes.
        throw new NotImplementedException(
            "Konsolenzugriff erfordert ein RDP-/WebSocket-Gateway und ist im aktuellen Scope nicht umgesetzt.");

    public async Task ResizeRamAsync(string id, int ramMb)
    {
        _logger.LogInformation("Setze RAM von VM {VmId} auf {RamMb} MB", id, ramMb);
        await InvokeAsync(ps => ps
            .AddCommand("Set-VM")
            .AddParameter("Name", id)
            .AddParameter("MemoryStartupBytes", ramMb * BytesPerMegabyte));
    }

    public async Task ResizeCpuAsync(string id, int cpuCount)
    {
        _logger.LogInformation("Setze CPU-Anzahl von VM {VmId} auf {CpuCount}", id, cpuCount);
        await InvokeAsync(ps => ps
            .AddCommand("Set-VM")
            .AddParameter("Name", id)
            .AddParameter("ProcessorCount", cpuCount));
    }

    public async Task AttachNetworkAdapterAsync(string id, string switchName)
    {
        _logger.LogInformation("Füge VM {VmId} einen Netzwerkadapter am Switch {SwitchName} hinzu", id, switchName);
        await InvokeAsync(ps => ps
            .AddCommand("Add-VMNetworkAdapter")
            .AddParameter("VMName", id)
            .AddParameter("SwitchName", switchName));
    }

    public async Task ResizeVhdAsync(string id, int sizeGb)
    {
        var vhdPath = await GetFirstVhdPathAsync(id);
        _logger.LogInformation("Vergrößere VHD {VhdPath} von VM {VmId} auf {SizeGb} GB", vhdPath, id, sizeGb);
        await InvokeAsync(ps => ps
            .AddCommand("Resize-VHD")
            .AddParameter("Path", vhdPath)
            .AddParameter("SizeBytes", sizeGb * BytesPerGigabyte));
    }

    public async Task CompactVhdAsync(string id)
    {
        var vhdPath = await GetFirstVhdPathAsync(id);
        _logger.LogInformation("Kompaktiere VHD {VhdPath} von VM {VmId}", vhdPath, id);
        await InvokeAsync(ps => ps
            .AddCommand("Optimize-VHD")
            .AddParameter("Path", vhdPath));
    }

    public async Task ExportVmAsync(string id, string exportPath)
    {
        _logger.LogInformation("Exportiere VM {VmId} nach {ExportPath}", id, exportPath);
        await InvokeAsync(ps => ps
            .AddCommand("Export-VM")
            .AddParameter("Name", id)
            .AddParameter("Path", exportPath));
    }

    public async Task ImportVmAsync(string importPath)
    {
        _logger.LogInformation("Importiere VM aus {ImportPath}", importPath);
        await InvokeAsync(ps => ps
            .AddCommand("Import-VM")
            .AddParameter("Path", importPath));
    }

    public Task CloneVmAsync(string id, string newName) =>
        // Hyper-V bietet kein 1:1-Cmdlet zum Klonen; ein Klon wäre eine
        // Export-/Import-Kombination mit Kopiersemantik und eigener Fehlerbehandlung —
        // das geht über den aktuellen Scope hinaus.
        throw new NotImplementedException(
            "Klonen erfordert eine Export-/Import-Kombination und ist im aktuellen Scope nicht umgesetzt.");

    public Task LiveMigrateVmAsync(string id, string targetHost) =>
        // Move-VM setzt einen zweiten Hyper-V-Host bzw. Cluster-Infrastruktur mit
        // Live-Migration-Konfiguration voraus; die Testumgebung ist ein Einzelhost.
        throw new NotImplementedException(
            "Live-Migration erfordert Cluster-Infrastruktur und ist in der Einzelhost-Umgebung nicht umgesetzt.");

    private async Task<string> GetFirstVhdPathAsync(string id)
    {
        var results = await InvokeAsync(ps => ps
            .AddCommand("Get-VMHardDiskDrive")
            .AddParameter("VMName", id));

        var firstDisk = results.FirstOrDefault()
            ?? throw new VirtualizationException($"VM {id} besitzt keine virtuelle Festplatte");

        return GetProperty(firstDisk, "Path");
    }

    private async Task<Collection<PSObject>> InvokeAsync(Action<PowerShell> configure)
    {
        Runspace? runspace = null;
        PowerShell? powerShell = null;

        try
        {
            // CreateDefault2 lädt nur die Core-Cmdlets aus System.Management.Automation und
            // vermeidet damit die Abhängigkeit zum Konsolen-Host. Das Hyper-V-Modul wird auf
            // dem Windows-Host bei Bedarf automatisch über den PSModulePath nachgeladen.
            runspace = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault2());
            runspace.Open();

            powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            configure(powerShell);

            var results = await Task.Run(() => powerShell.Invoke());

            if (powerShell.HadErrors)
            {
                var firstError = powerShell.Streams.Error.FirstOrDefault();
                throw new VirtualizationException(
                    $"Hyper-V-Befehl fehlgeschlagen: {firstError?.Exception.Message ?? "unbekannter Fehler"}",
                    firstError?.Exception);
            }

            return results;
        }
        catch (VirtualizationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lokale Hyper-V-Ausführung fehlgeschlagen");
            throw new VirtualizationException(
                $"Lokale Hyper-V-Ausführung nicht möglich: {ex.Message}", ex);
        }
        finally
        {
            powerShell?.Dispose();
            runspace?.Dispose();
        }
    }

    private static VirtualMachine MapVm(PSObject psObject) => new()
    {
        Id = GetProperty(psObject, "Name"),
        Name = GetProperty(psObject, "Name"),
        Status = MapStatus(GetProperty(psObject, "State")),
        // Zuordnung VM → Benutzer wird im Hyper-V-Notizfeld gepflegt (z. B. Notes = "mugur").
        // Eine persistente Zuordnung über eine Datenbank folgt in Phase 5.
        AssignedUserId = GetProperty(psObject, "Notes").Trim()
    };

    private static string GetProperty(PSObject psObject, string name) =>
        psObject.Properties[name]?.Value?.ToString() ?? string.Empty;

    private static VmStatus MapStatus(string state) => state switch
    {
        "Running" => VmStatus.Running,
        "Off" => VmStatus.Stopped,
        "Paused" => VmStatus.Paused,
        _ => VmStatus.Unknown
    };
}
