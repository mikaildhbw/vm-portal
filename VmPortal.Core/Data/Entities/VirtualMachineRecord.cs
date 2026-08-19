namespace VmPortal.Core.Data.Entities;

/// <summary>
/// Autorisierungs-Metadaten zu einer VM. Heißt "VirtualMachineRecord" statt "VirtualMachine",
/// um nicht mit dem Laufzeitmodell <see cref="Models.VirtualMachine"/> des jeweiligen
/// Virtualisierungs-Providers zu kollidieren; die Datenbanktabelle heißt weiterhin
/// "VirtualMachines" (siehe <see cref="VmPortalDbContext"/>).
/// </summary>
public class VirtualMachineRecord
{
    public int Id { get; set; }
    public int ServerId { get; set; }
    public VirtualServer Server { get; set; } = null!;

    /// <summary>
    /// Ohne Gruppe ist die VM bewusst unsichtbar/nicht zugreifbar (secure-by-default) -
    /// das ist kein Fehlerfall, sondern der Ausgangszustand neu erfasster VMs.
    /// </summary>
    public int? GroupId { get; set; }
    public VirtualMachineGroup? Group { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Hyper-V-VM-GUID (Get-VM-Attribut "Id"), optional. Wird beim Anlegen/Aktualisieren über
    /// die Admin-API (VM-Gruppen-Mitgliedschaft, siehe VmGroupsController) aus der
    /// Live-Hypervisor-Antwort übernommen, falls bekannt - dient nur der Nachvollziehbarkeit/
    /// Robustheit gegen VM-Umbenennung, NICHT dem Autorisierungs-Abgleich in
    /// DbAuthorizationService (der bleibt namens-/host-basiert, siehe docs/authorization.md).
    /// </summary>
    public string? VmGuid { get; set; }
}
