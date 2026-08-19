using VmPortal.Core.Models;

namespace VmPortal.Core.Interfaces;

/// <summary>
/// Ermittelt die per Datenbank-Autorisierungsschicht (RBAC über Rollen und
/// GroupPermissions) erlaubten Aktionen eines Nutzers auf einer VM. Siehe
/// docs/authorization.md für die Herleitung der Regeln.
/// </summary>
public interface IDbAuthorizationService
{
    /// <summary>
    /// Alle Aktionen, die der Nutzer (über seine AD-Gruppen) auf der angegebenen VM
    /// ausführen darf - Union aller RoleActions über alle zutreffenden Rollen.
    /// Leere Menge, wenn die VM keiner VM-Gruppe zugeordnet ist oder keine
    /// GroupPermission zutrifft.
    /// </summary>
    /// <param name="hostName">
    /// Name des Hyper-V-Hosts (<see cref="Models.VirtualMachine.HostName"/>), auf dem die VM
    /// läuft. Optional für Rückwärtskompatibilität (Dummy-Provider/lokaler Modus liefern keinen
    /// Hostnamen), aber notwendig für korrekte Autorisierung im Remote-Multi-Host-Modus: VM-Namen
    /// sind dort nicht eindeutig (bestätigte Kollisionen zwischen Hosts, siehe
    /// docs/PROJEKT_ERKLAERUNG.md) - ohne Host-Filter würde die Autorisierung eine von mehreren
    /// gleichnamigen VMs beliebig auswählen.
    /// </param>
    Task<IReadOnlySet<VmAction>> GetAllowedActionsAsync(
        IReadOnlyCollection<string> adGroups, string vmName, string? hostName = null);

    Task<bool> IsAllowedAsync(
        IReadOnlyCollection<string> adGroups, string vmName, VmAction action, string? hostName = null);

    /// <summary>
    /// True, wenn eine der AD-Gruppen des Nutzers der konfigurierte Bootstrap-FullAdmin-Gruppe
    /// entspricht (Authorization:BootstrapFullAdminGroup). Wird u. a. von den
    /// Admin-Endpunkten genutzt, die global FullAdmin voraussetzen.
    /// </summary>
    bool IsBootstrapFullAdmin(IReadOnlyCollection<string> adGroups);

    /// <summary>
    /// Ermittelt in EINER Abfrage, welche VMs der Nutzer (über seine AD-Gruppen) für die
    /// angegebene Aktion sehen darf - Gegenstück zum pro-VM-Aufruf von
    /// <see cref="GetAllowedActionsAsync"/>/<see cref="IsAllowedAsync"/>, gedacht für Listen
    /// (z. B. die VM-Übersicht), wo eine Autorisierungsprüfung pro einzelner VM zu N+1
    /// DB-Roundtrips führen würde. Berücksichtigt NICHT den Bootstrap-FullAdmin-Fall (der hat
    /// keine einschränkende GroupPermission-Zeile, gegen die man vorab filtern könnte) -
    /// Aufrufer müssen <see cref="IsBootstrapFullAdmin"/> selbst vorab prüfen und in dem Fall
    /// das volle Hypervisor-Inventar abfragen statt diese Methode zu nutzen.
    /// </summary>
    Task<IReadOnlyList<VmReference>> GetAuthorizedVmsAsync(IReadOnlyCollection<string> adGroups, VmAction action);
}
