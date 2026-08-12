namespace VmPortal.Core.Data.Entities;

/// <summary>
/// Datenbank-Abbild eines Werts aus <see cref="Models.VmAction"/> (Name = Enum-Wert als
/// String). Heißt "VmActionEntity" statt "VmAction", um nicht mit dem Enum zu kollidieren;
/// die Datenbanktabelle heißt weiterhin "VMActions" (siehe <see cref="VmPortalDbContext"/>).
/// </summary>
public class VmActionEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<RoleAction> RoleActions { get; set; } = new();
}
