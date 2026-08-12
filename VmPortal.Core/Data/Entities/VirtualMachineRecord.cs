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
}
