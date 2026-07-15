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
