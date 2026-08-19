using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Management.Automation.Runspaces;
using Microsoft.Extensions.Logging;
using VmPortal.Core.Configuration;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Models;

namespace VmPortal.Core.Services;

/// <summary>
/// Konkrete Umsetzung von <see cref="IVirtualizationProvider"/> für Microsoft Hyper-V.
/// Unterstützt zwei Modi (<see cref="HyperVSettings.Mode"/>):
/// <list type="bullet">
/// <item><see cref="HyperVMode.Local"/> - die App läuft direkt auf dem Hyper-V-Host, die
/// Cmdlets werden in-process über eine lokale PowerShell-Instanz ausgeführt (ursprüngliches,
/// unverändertes Verhalten, kein WinRM nötig).</item>
/// <item><see cref="HyperVMode.Remote"/> - die App läuft auf einem separaten Server und steuert
/// die in <see cref="HyperVSettings.Hosts"/> konfigurierten Hyper-V-Hosts über
/// PowerShell-Remoting (WinRM/Kerberos) an. Pro Host wird ein <see cref="RunspacePool"/>
/// über die Lebensdauer der Anwendung wiederverwendet (der Provider ist als Singleton
/// registriert), da Runspace-Aufbau pro Aufruf bei WinRM spürbar teurer ist als lokal.</item>
/// </list>
/// Die Abstraktion über das Interface erlaubt es, denselben Portal-Kern gegen andere
/// Hypervisoren (z. B. Proxmox) zu betreiben, ohne die API-Schicht zu ändern - das ist der
/// plattformunabhängige Kern der Arbeit.
/// </summary>
public class HyperVProvider : IVirtualizationProvider
{
    private const long BytesPerMegabyte = 1024L * 1024L;
    private const long BytesPerGigabyte = 1024L * 1024L * 1024L;

    /// <summary>
    /// Trennzeichen zwischen Hostname und VM-GUID in <see cref="VirtualMachine.Id"/> im
    /// Remote-Modus. Kommt weder in Hyper-V-Hostnamen (DNS-Namen) noch in GUIDs vor.
    /// </summary>
    private const string HostGuidSeparator = "::";

    private readonly ILogger<HyperVProvider> _logger;
    private readonly HyperVSettings _settings;

    // Ein RunspacePool pro konfiguriertem Host im Remote-Modus. Lazy<T> stellt sicher, dass
    // der Pool pro Host trotz paralleler erster Zugriffe nur genau einmal erstellt/geöffnet
    // wird (ConcurrentDictionary.GetOrAdd allein garantiert das für den Factory-Delegaten
    // selbst nicht).
    private readonly ConcurrentDictionary<string, Lazy<RunspacePool>> _hostPools =
        new(StringComparer.OrdinalIgnoreCase);

    public HyperVProvider(HyperVSettings settings, ILogger<HyperVProvider> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<IEnumerable<VirtualMachine>> GetVmsAsync()
    {
        if (_settings.Mode != HyperVMode.Remote)
        {
            _logger.LogInformation("Rufe alle VMs vom lokalen Hyper-V-Host ab");
            var localResults = await InvokeLocalAsync(ps => ps.AddCommand("Get-VM"));
            return localResults.Select(vm => MapVm(string.Empty, vm)).ToList();
        }

        var allVms = new List<VirtualMachine>();
        foreach (var host in _settings.Hosts)
        {
            try
            {
                _logger.LogInformation("Rufe VMs von Host {HostName} ab", host.Name);
                var results = await InvokeOnHostAsync(host.Name, ps => ps.AddCommand("Get-VM"));
                allVms.AddRange(results.Select(vm => MapVm(host.Name, vm)));
            }
            catch (Exception ex)
            {
                // Multi-Host-Aggregation ist pro Host fehlertolerant: ein nicht erreichbarer
                // oder fehlerhafter Host darf die Ergebnisse der anderen Hosts nicht verhindern.
                _logger.LogError(ex, "Host {HostName} bei Get-VM übersprungen (nicht erreichbar oder Fehler)", host.Name);
            }
        }

        return allVms;
    }

    public async Task<IEnumerable<VirtualMachine>> GetVmsAsync(IReadOnlyCollection<VmReference> authorizedVms)
    {
        if (authorizedVms.Count == 0)
            return Enumerable.Empty<VirtualMachine>();

        if (_settings.Mode != HyperVMode.Remote)
        {
            // Lokaler Modus: ein impliziter Host - trotzdem gezielt per Namensliste statt
            // des kompletten Get-VM ohne Filter.
            var localNames = authorizedVms.Select(v => v.Name).Distinct().ToArray();
            _logger.LogInformation("Rufe {Count} autorisierte VMs vom lokalen Hyper-V-Host ab", localNames.Length);
            var localResults = await InvokeLocalAsync(ps => ps
                .AddCommand("Get-VM")
                .AddParameter("Name", localNames)
                .AddParameter("ErrorAction", "SilentlyContinue"));
            return localResults.Select(vm => MapVm(string.Empty, vm)).ToList();
        }

        var allVms = new List<VirtualMachine>();
        foreach (var hostGroup in authorizedVms.GroupBy(v => v.HostName, StringComparer.OrdinalIgnoreCase))
        {
            var hostName = hostGroup.Key;
            var names = hostGroup.Select(v => v.Name).Distinct().ToArray();

            try
            {
                // EIN Get-VM mit Namensliste pro Host statt eines vollen, ungefilterten
                // Inventar-Abrufs - Hosts ohne autorisierte VMs tauchen in authorizedVms gar
                // nicht erst auf und werden dadurch automatisch übersprungen.
                _logger.LogInformation("Rufe {Count} autorisierte VMs von Host {HostName} ab", names.Length, hostName);
                var results = await InvokeOnHostAsync(hostName, ps => ps
                    .AddCommand("Get-VM")
                    .AddParameter("Name", names)
                    .AddParameter("ErrorAction", "SilentlyContinue"));
                allVms.AddRange(results.Select(vm => MapVm(hostName, vm)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Host {HostName} bei gezielter VM-Abfrage übersprungen (nicht erreichbar oder Fehler)", hostName);
            }
        }

        return allVms;
    }

    public async Task<VirtualMachine?> GetVmByIdAsync(string id)
    {
        if (_settings.Mode != HyperVMode.Remote)
        {
            _logger.LogInformation("Rufe VM {VmId} vom lokalen Hyper-V-Host ab", id);
            var localResults = await InvokeLocalAsync(ps => ps
                .AddCommand("Get-VM")
                .AddParameter("Name", id)
                .AddParameter("ErrorAction", "SilentlyContinue"));

            var localVm = localResults.FirstOrDefault();
            return localVm is null ? null : MapVm(string.Empty, localVm);
        }

        if (!TryParseCompositeId(id, out var hostName, out var vmGuid))
        {
            _logger.LogWarning(
                "VM-Id '{VmId}' hat im Remote-Modus nicht das erwartete Format 'Host{Sep}Guid'",
                id, HostGuidSeparator);
            return null;
        }

        _logger.LogInformation("Rufe VM {VmGuid} von Host {HostName} ab", vmGuid, hostName);
        var results = await InvokeOnHostAsync(hostName, ps => ps
            .AddCommand("Get-VM")
            .AddParameter("Id", vmGuid)
            .AddParameter("ErrorAction", "SilentlyContinue"));

        var vm = results.FirstOrDefault();
        return vm is null ? null : MapVm(hostName, vm);
    }

    public Task StartVmAsync(string id)
    {
        _logger.LogInformation("Starte VM {VmId}", id);
        return InvokeOnVmAsync(id, "Start-VM", ps => { });
    }

    public Task StopVmAsync(string id)
    {
        _logger.LogInformation("Stoppe VM {VmId}", id);
        return InvokeOnVmAsync(id, "Stop-VM", ps => ps.AddParameter("Force", true));
    }

    public Task ResetVmAsync(string id)
    {
        _logger.LogInformation("Starte VM {VmId} neu", id);
        return InvokeOnVmAsync(id, "Restart-VM", ps => ps.AddParameter("Force", true));
    }

    public Task CreateSnapshotAsync(string id, string snapshotName)
    {
        _logger.LogInformation("Erstelle Snapshot {SnapshotName} für VM {VmId}", snapshotName, id);
        return InvokeOnVmAsync(id, "Checkpoint-VM", ps => ps.AddParameter("SnapshotName", snapshotName));
    }

    public async Task<VmMeteringData?> GetMeteringAsync(string id)
    {
        _logger.LogInformation("Rufe Metering-Daten für VM {VmId} ab", id);
        var results = await InvokeOnVmAsync(id, "Measure-VM", ps => { });

        var report = results.FirstOrDefault();
        return report is null
            ? null
            : new VmMeteringData(
                GetProperty(report, "AvgCPU"),
                GetProperty(report, "AvgRAM"),
                GetProperty(report, "TotalDisk"));
    }

    public Task PauseVmAsync(string id)
    {
        _logger.LogInformation("Pausiere VM {VmId}", id);
        return InvokeOnVmAsync(id, "Suspend-VM", ps => { });
    }

    public Task ResumeVmAsync(string id)
    {
        _logger.LogInformation("Setze VM {VmId} fort", id);
        return InvokeOnVmAsync(id, "Resume-VM", ps => { });
    }

    public Task SaveStateAsync(string id)
    {
        _logger.LogInformation("Speichere Zustand von VM {VmId}", id);
        return InvokeOnVmAsync(id, "Save-VM", ps => { });
    }

    public Task ApplySnapshotAsync(string id, string snapshotName)
    {
        _logger.LogInformation("Wende Snapshot {SnapshotName} auf VM {VmId} an", snapshotName, id);
        return InvokeOnVmAsync(id, "Restore-VMSnapshot", ps => ps
            .AddParameter("Name", snapshotName)
            .AddParameter("Confirm", false));
    }

    public Task DeleteSnapshotAsync(string id, string snapshotName)
    {
        _logger.LogInformation("Lösche Snapshot {SnapshotName} von VM {VmId}", snapshotName, id);
        return InvokeOnVmAsync(id, "Remove-VMSnapshot", ps => ps
            .AddParameter("Name", snapshotName)
            .AddParameter("Confirm", false));
    }

    public Task<string> GetConsoleConnectionAsync(string id) =>
        // Es gibt kein Hyper-V-Cmdlet für Konsolenzugriff: vmconnect.exe ist eine
        // GUI-Anwendung und liefert keinen Stream, den ein Web-Portal durchreichen
        // könnte. Eine Web-Konsole erfordert zusätzliche Infrastruktur
        // (z. B. RDP-/WebSocket-Gateway) und liegt außerhalb des aktuellen Scopes.
        // Gilt unverändert für Local- und Remote-Modus.
        throw new NotImplementedException(
            "Konsolenzugriff erfordert ein RDP-/WebSocket-Gateway und ist im aktuellen Scope nicht umgesetzt.");

    public Task ResizeRamAsync(string id, int ramMb)
    {
        _logger.LogInformation("Setze RAM von VM {VmId} auf {RamMb} MB", id, ramMb);
        return InvokeOnVmAsync(id, "Set-VM", ps => ps.AddParameter("MemoryStartupBytes", ramMb * BytesPerMegabyte));
    }

    public Task ResizeCpuAsync(string id, int cpuCount)
    {
        _logger.LogInformation("Setze CPU-Anzahl von VM {VmId} auf {CpuCount}", id, cpuCount);
        return InvokeOnVmAsync(id, "Set-VM", ps => ps.AddParameter("ProcessorCount", cpuCount));
    }

    public Task AttachNetworkAdapterAsync(string id, string switchName)
    {
        _logger.LogInformation("Füge VM {VmId} einen Netzwerkadapter am Switch {SwitchName} hinzu", id, switchName);
        return InvokeOnVmAsync(id, "Add-VMNetworkAdapter", ps => ps.AddParameter("SwitchName", switchName));
    }

    public async Task ResizeVhdAsync(string id, int sizeGb)
    {
        var vhdPath = await GetFirstVhdPathAsync(id);
        _logger.LogInformation("Vergrößere VHD {VhdPath} von VM {VmId} auf {SizeGb} GB", vhdPath, id, sizeGb);
        await InvokeOnHostOfVmAsync(id, ps => ps
            .AddCommand("Resize-VHD")
            .AddParameter("Path", vhdPath)
            .AddParameter("SizeBytes", sizeGb * BytesPerGigabyte));
    }

    public async Task CompactVhdAsync(string id)
    {
        var vhdPath = await GetFirstVhdPathAsync(id);
        _logger.LogInformation("Kompaktiere VHD {VhdPath} von VM {VmId}", vhdPath, id);
        await InvokeOnHostOfVmAsync(id, ps => ps
            .AddCommand("Optimize-VHD")
            .AddParameter("Path", vhdPath));
    }

    public Task ExportVmAsync(string id, string exportPath)
    {
        _logger.LogInformation("Exportiere VM {VmId} nach {ExportPath}", id, exportPath);
        return InvokeOnVmAsync(id, "Export-VM", ps => ps.AddParameter("Path", exportPath));
    }

    public Task ImportVmAsync(string importPath)
    {
        if (_settings.Mode == HyperVMode.Remote)
            // IVirtualizationProvider.ImportVmAsync bekommt nur den Importpfad, keinen
            // Zielhost - im Remote-Multi-Host-Modus lässt sich daraus kein eindeutiger Host
            // ableiten. Bewusst nicht implementiert statt eines Hosts zu raten; siehe
            // Abschlussbericht zu dieser Aufgabe (offener Punkt).
            throw new NotImplementedException(
                "Import ist im Remote-Multi-Host-Modus nicht umgesetzt: die Methode erhält keinen " +
                "Zielhost-Parameter, eine Host-Auswahl wäre daher Raten statt Absicht.");

        _logger.LogInformation("Importiere VM aus {ImportPath}", importPath);
        return InvokeLocalAsync(ps => ps.AddCommand("Import-VM").AddParameter("Path", importPath));
    }

    public Task CloneVmAsync(string id, string newName) =>
        // Hyper-V bietet kein 1:1-Cmdlet zum Klonen; ein Klon wäre eine
        // Export-/Import-Kombination mit Kopiersemantik und eigener Fehlerbehandlung -
        // das geht über den aktuellen Scope hinaus. Gilt unverändert für beide Modi.
        throw new NotImplementedException(
            "Klonen erfordert eine Export-/Import-Kombination und ist im aktuellen Scope nicht umgesetzt.");

    public Task LiveMigrateVmAsync(string id, string targetHost) =>
        // Move-VM setzt eine Cluster-Konfiguration mit Live-Migration-Unterstützung voraus,
        // die über die reine WinRM-Ansteuerung mehrerer Einzelhosts hinausgeht - bewusst
        // nicht umgesetzt, unabhängig vom Modus.
        throw new NotImplementedException(
            "Live-Migration erfordert Cluster-Infrastruktur und ist nicht umgesetzt.");

    private async Task<string> GetFirstVhdPathAsync(string id)
    {
        var results = await InvokeOnVmAsync(id, "Get-VMHardDiskDrive", ps => { });

        var firstDisk = results.FirstOrDefault()
            ?? throw new VirtualizationException($"VM {id} besitzt keine virtuelle Festplatte");

        return GetProperty(firstDisk, "Path");
    }

    /// <summary>
    /// Führt einen Cmdlet-Namen gezielt auf der durch <paramref name="id"/> identifizierten VM
    /// aus. Lokal: <paramref name="commandName"/> -Name id (bisheriges Verhalten). Remote:
    /// Get-VM -Id vmGuid | <paramref name="commandName"/> auf dem aus id ermittelten Host -
    /// die Hyper-V-Cmdlets sind darauf ausgelegt, das per Pipeline übergebene VM-Objekt aus
    /// Get-VM zu akzeptieren, wodurch dieselbe GUID-genaue Zielauswahl über die gesamte
    /// Cmdlet-Familie hinweg funktioniert.
    /// </summary>
    private Task<Collection<PSObject>> InvokeOnVmAsync(string id, string commandName, Action<PowerShell> configureCommand)
    {
        if (_settings.Mode != HyperVMode.Remote)
        {
            return InvokeLocalAsync(ps =>
            {
                ps.AddCommand(commandName).AddParameter("Name", id);
                configureCommand(ps);
            });
        }

        if (!TryParseCompositeId(id, out var hostName, out var vmGuid))
            throw new VirtualizationException(
                $"VM-Id '{id}' hat im Remote-Modus nicht das erwartete Format 'Host{HostGuidSeparator}Guid'");

        return InvokeOnHostAsync(hostName, ps =>
        {
            ps.AddCommand("Get-VM").AddParameter("Id", vmGuid);
            ps.AddCommand(commandName);
            configureCommand(ps);
        });
    }

    /// <summary>
    /// Führt einen host-lokalen Befehl (kein VM-Pipeline-Objekt nötig, z. B. Resize-VHD über
    /// einen bereits aufgelösten Dateipfad) auf dem aus <paramref name="id"/> ermittelten Host
    /// aus - lokal auf dem einzigen impliziten Host, remote auf dem aus der Id geparsten Host.
    /// </summary>
    private Task<Collection<PSObject>> InvokeOnHostOfVmAsync(string id, Action<PowerShell> configure)
    {
        if (_settings.Mode != HyperVMode.Remote)
            return InvokeLocalAsync(configure);

        if (!TryParseCompositeId(id, out var hostName, out _))
            throw new VirtualizationException(
                $"VM-Id '{id}' hat im Remote-Modus nicht das erwartete Format 'Host{HostGuidSeparator}Guid'");

        return InvokeOnHostAsync(hostName, configure);
    }

    private async Task<Collection<PSObject>> InvokeLocalAsync(Action<PowerShell> configure)
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

    private async Task<Collection<PSObject>> InvokeOnHostAsync(string hostName, Action<PowerShell> configure)
    {
        var hostSettings = _settings.Hosts.FirstOrDefault(h => string.Equals(h.Name, hostName, StringComparison.OrdinalIgnoreCase))
            ?? throw new VirtualizationException(
                $"Unbekannter Hyper-V-Host '{hostName}' (nicht in Virtualization:HyperV:Hosts konfiguriert)");

        var pool = GetOrCreatePool(hostSettings);
        PowerShell? powerShell = null;

        try
        {
            powerShell = PowerShell.Create();
            powerShell.RunspacePool = pool;
            configure(powerShell);

            var results = await Task.Run(() => powerShell.Invoke());

            if (powerShell.HadErrors)
            {
                var firstError = powerShell.Streams.Error.FirstOrDefault();
                throw new VirtualizationException(
                    $"Hyper-V-Befehl auf Host {hostName} fehlgeschlagen: {firstError?.Exception.Message ?? "unbekannter Fehler"}",
                    firstError?.Exception);
            }

            return results;
        }
        catch (VirtualizationException)
        {
            throw;
        }
        catch (PSRemotingTransportException ex)
        {
            // WinRM-Verbindungsfehler (Host down, Netzwerkproblem, Kerberos-Ticket abgelaufen
            // o. Ä.): Pool für diesen Host verwerfen, damit der nächste Zugriff einen frischen
            // Verbindungsaufbau versucht, statt dauerhaft an einer defekten Verbindung
            // hängenzubleiben ("Reconnect beim nächsten Zugriff").
            _logger.LogError(ex, "WinRM-Verbindung zu Host {HostName} unterbrochen - Pool wird verworfen", hostName);
            InvalidatePool(hostName);
            throw new VirtualizationException(
                $"WinRM-Verbindung zu Host {hostName} fehlgeschlagen (Transportfehler): {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote-Hyper-V-Ausführung auf Host {HostName} fehlgeschlagen", hostName);
            throw new VirtualizationException(
                $"Remote-Hyper-V-Ausführung auf Host {hostName} nicht möglich: {ex.Message}", ex);
        }
        finally
        {
            powerShell?.Dispose();
        }
    }

    private RunspacePool GetOrCreatePool(HyperVHostSettings hostSettings) =>
        _hostPools.GetOrAdd(hostSettings.Name, _ => new Lazy<RunspacePool>(() => CreateAndOpenPool(hostSettings))).Value;

    private RunspacePool CreateAndOpenPool(HyperVHostSettings hostSettings)
    {
        var authMechanism = Enum.Parse<AuthenticationMechanism>(_settings.Remote.Authentication, ignoreCase: true);

        // credential: null -> Authentifizierung über die Prozessidentität des ausführenden
        // Kontos (Kerberos/SSPI), passend zur manuell verifizierten Konnektivität gegen alle
        // drei Ziel-Hosts ohne Sonderkonfiguration.
        var connectionInfo = new WSManConnectionInfo(
            useSsl: _settings.Remote.UseSsl,
            computerName: hostSettings.FQDN,
            port: _settings.Remote.Port,
            appName: "/wsman",
            shellUri: "http://schemas.microsoft.com/powershell/Microsoft.PowerShell",
            credential: null)
        {
            AuthenticationMechanism = authMechanism
        };

        var pool = RunspaceFactory.CreateRunspacePool(minRunspaces: 1, maxRunspaces: 5, connectionInfo);
        pool.Open();

        _logger.LogInformation(
            "WinRM-RunspacePool für Host {HostName} ({Fqdn}:{Port}) geöffnet",
            hostSettings.Name, hostSettings.FQDN, _settings.Remote.Port);

        return pool;
    }

    private void InvalidatePool(string hostName)
    {
        if (!_hostPools.TryRemove(hostName, out var removed) || !removed.IsValueCreated)
            return;

        try
        {
            removed.Value.Close();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Fehler beim Schließen des defekten RunspacePool für Host {HostName} (ignoriert)", hostName);
        }
        finally
        {
            removed.Value.Dispose();
        }
    }

    private static bool TryParseCompositeId(string id, out string hostName, out Guid vmGuid)
    {
        hostName = string.Empty;
        vmGuid = Guid.Empty;

        var separatorIndex = id.IndexOf(HostGuidSeparator, StringComparison.Ordinal);
        if (separatorIndex < 0)
            return false;

        hostName = id[..separatorIndex];
        var guidPart = id[(separatorIndex + HostGuidSeparator.Length)..];
        return Guid.TryParse(guidPart, out vmGuid);
    }

    private static string BuildCompositeId(string hostName, Guid vmGuid) =>
        $"{hostName}{HostGuidSeparator}{vmGuid}";

    private static VirtualMachine MapVm(string hostName, PSObject psObject)
    {
        var name = GetProperty(psObject, "Name");
        var hasGuid = Guid.TryParse(GetProperty(psObject, "Id"), out var vmGuid);

        return new VirtualMachine
        {
            // Im lokalen Modus (hostName leer) bleibt Id unverändert der VM-Name - bestehendes
            // Verhalten. Im Remote-Modus die host-eindeutige Kombination aus Hostname und
            // Hyper-V-VM-GUID, da Namen über Hosts hinweg kollidieren können.
            Id = hasGuid && !string.IsNullOrEmpty(hostName) ? BuildCompositeId(hostName, vmGuid) : name,
            Name = name,
            Status = MapStatus(GetProperty(psObject, "State")),
            // Zuordnung VM → Benutzer wird im Hyper-V-Notizfeld gepflegt (z. B. Notes = "mugur").
            AssignedUserId = GetProperty(psObject, "Notes").Trim(),
            HostName = hostName,
            VmGuid = hasGuid ? vmGuid.ToString() : string.Empty
        };
    }

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
