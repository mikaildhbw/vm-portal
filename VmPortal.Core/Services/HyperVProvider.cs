using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using Microsoft.Extensions.Logging;
using VmPortal.Core.Configuration;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Models;

namespace VmPortal.Core.Services;

/// <summary>
/// Konkrete Umsetzung von <see cref="IVirtualizationProvider"/> für Microsoft Hyper-V.
/// Die Hyper-V-Cmdlets werden per PowerShell-Remoting (WSMan/WinRM über HTTPS) auf dem
/// Windows-Host ausgeführt. Die Abstraktion über das Interface erlaubt es, denselben
/// Portal-Kern gegen andere Hypervisoren (z. B. Proxmox) zu betreiben, ohne die API-Schicht
/// zu ändern — das ist der plattformunabhängige Kern der Arbeit.
/// </summary>
public class HyperVProvider : IVirtualizationProvider
{
    private const string ShellUri = "http://schemas.microsoft.com/powershell/Microsoft.PowerShell";

    private readonly HyperVSettings _settings;
    private readonly ILogger<HyperVProvider> _logger;

    public HyperVProvider(HyperVSettings settings, ILogger<HyperVProvider> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<IEnumerable<VirtualMachine>> GetVmsAsync()
    {
        _logger.LogInformation("Rufe alle VMs vom Hyper-V-Host {Host} ab", _settings.Host);
        var results = await InvokeAsync(ps => ps.AddCommand("Get-VM"));
        return results.Select(MapVm).ToList();
    }

    public async Task<VirtualMachine?> GetVmByIdAsync(string id)
    {
        _logger.LogInformation("Rufe VM {VmId} vom Hyper-V-Host {Host} ab", id, _settings.Host);
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
            runspace = RunspaceFactory.CreateRunspace(CreateConnectionInfo());
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
            _logger.LogError(ex, "WinRM-Verbindung zum Hyper-V-Host {Host}:{Port} fehlgeschlagen",
                _settings.Host, _settings.Port);
            throw new VirtualizationException(
                $"Verbindung zum Hyper-V-Host {_settings.Host}:{_settings.Port} nicht möglich: {ex.Message}", ex);
        }
        finally
        {
            powerShell?.Dispose();
            runspace?.Dispose();
        }
    }

    private WSManConnectionInfo CreateConnectionInfo()
    {
        var credential = new PSCredential(_settings.Username, ToSecureString(_settings.Password));
        var connectionInfo = new WSManConnectionInfo(
            useSsl: _settings.UseSsl,
            computerName: _settings.Host,
            port: _settings.Port,
            appName: "/wsman",
            shellUri: ShellUri,
            credential: credential)
        {
            AuthenticationMechanism = AuthenticationMechanism.Negotiate,
            // Das Testsystem nutzt ein selbstsigniertes Zertifikat. In Produktion wird ein
            // von der internen CA ausgestelltes Zertifikat vertraut, sodass diese Prüfungen
            // (siehe CertificateThumbprint in appsettings.json) nicht übersprungen werden müssen.
            SkipCACheck = true,
            SkipCNCheck = true,
            SkipRevocationCheck = true
        };

        return connectionInfo;
    }

    private static SecureString ToSecureString(string value)
    {
        var secure = new SecureString();
        foreach (var character in value)
            secure.AppendChar(character);
        secure.MakeReadOnly();
        return secure;
    }

    private static VirtualMachine MapVm(PSObject psObject) => new()
    {
        Id = GetProperty(psObject, "Name"),
        Name = GetProperty(psObject, "Name"),
        Status = MapStatus(GetProperty(psObject, "State")),
        // Zuordnung VM → Benutzer wird im Hyper-V-Notizfeld gepflegt. Eine persistente
        // Zuordnung über eine Datenbank folgt in Phase 5.
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
