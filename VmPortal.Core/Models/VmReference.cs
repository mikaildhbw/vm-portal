namespace VmPortal.Core.Models;

/// <summary>
/// Verweist auf eine VM anhand von Host + Name, ohne ein volles <see cref="VirtualMachine"/>
/// (mit Live-Status vom Hypervisor) zu sein. Verbindet die DB-Autorisierungsschicht
/// (<see cref="Interfaces.IDbAuthorizationService.GetAuthorizedVmsAsync"/>) mit dem
/// gezielten Hypervisor-Abruf (<see cref="Interfaces.IVirtualizationProvider.GetVmsAsync(System.Collections.Generic.IReadOnlyCollection{VmReference})"/>):
/// erst wird in der DB ermittelt, welche VMs der Nutzer sehen darf, danach wird nur für
/// genau diese beim Hypervisor nachgefragt - statt das komplette Inventar zu holen und
/// hinterher pro VM einzeln zu autorisieren.
/// </summary>
public record VmReference(string HostName, string Name);
