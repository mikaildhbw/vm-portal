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
    Task<IReadOnlySet<VmAction>> GetAllowedActionsAsync(IReadOnlyCollection<string> adGroups, string vmName);

    Task<bool> IsAllowedAsync(IReadOnlyCollection<string> adGroups, string vmName, VmAction action);

    /// <summary>
    /// True, wenn eine der AD-Gruppen des Nutzers der konfigurierte Bootstrap-FullAdmin-Gruppe
    /// entspricht (Authorization:BootstrapFullAdminGroup). Wird u. a. von den
    /// Admin-Endpunkten genutzt, die global FullAdmin voraussetzen.
    /// </summary>
    bool IsBootstrapFullAdmin(IReadOnlyCollection<string> adGroups);
}
