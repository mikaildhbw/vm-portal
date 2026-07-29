using System.Text.Json;
using System.Text.Json.Serialization;
using VmPortal.Core.Models;

namespace VmPortal.Core.Services;

/// <summary>
/// Serialisiert und parst den "vmroles"-Claim, der die VM->Rolle-Zuordnungen eines
/// Benutzers als JSON-Payload im JWT transportiert
/// (z. B. <c>[{"vm":"VM-Mikail","role":"PowerUser"}]</c>).
/// </summary>
public static class VmRoleClaims
{
    public const string ClaimType = "vmroles";

    private record VmRoleEntry(
        [property: JsonPropertyName("vm")] string Vm,
        [property: JsonPropertyName("role")] string Role);

    public static string Serialize(IReadOnlyDictionary<string, VmRole> vmRoles)
    {
        var entries = vmRoles.Select(pair => new VmRoleEntry(pair.Key, pair.Value.ToString()));
        return JsonSerializer.Serialize(entries);
    }

    /// <summary>
    /// Parst die vmroles-Claim-Werte eines ClaimsPrincipal. Wichtig: Der JWT-Handler
    /// zerlegt einen JSON-Array-Claim beim Validieren in einen Claim pro Array-Element
    /// (Wert ist dann ein einzelnes JSON-Objekt) — daher werden hier alle Claim-Werte
    /// entgegengenommen und sowohl Array- als auch Einzelobjekt-Form akzeptiert.
    /// </summary>
    public static IReadOnlyDictionary<string, VmRole> Deserialize(IEnumerable<string?> claimValues)
    {
        var vmRoles = new Dictionary<string, VmRole>(StringComparer.OrdinalIgnoreCase);

        foreach (var claimValue in claimValues)
        {
            foreach (var entry in ParseEntries(claimValue))
            {
                if (!Enum.TryParse<VmRole>(entry.Role, ignoreCase: true, out var role))
                    continue;

                // Bei mehrfachen Einträgen zur selben VM gilt die höchste Rolle.
                if (!vmRoles.TryGetValue(entry.Vm, out var existingRole) || role > existingRole)
                    vmRoles[entry.Vm] = role;
            }
        }

        return vmRoles;
    }

    public static IReadOnlyDictionary<string, VmRole> Deserialize(string? claimValue) =>
        Deserialize(new[] { claimValue });

    private static List<VmRoleEntry> ParseEntries(string? claimValue)
    {
        if (string.IsNullOrWhiteSpace(claimValue))
            return new List<VmRoleEntry>();

        try
        {
            if (claimValue.TrimStart().StartsWith('['))
                return JsonSerializer.Deserialize<List<VmRoleEntry>>(claimValue) ?? new List<VmRoleEntry>();

            var entry = JsonSerializer.Deserialize<VmRoleEntry>(claimValue);
            return entry is null ? new List<VmRoleEntry>() : new List<VmRoleEntry> { entry };
        }
        catch (JsonException)
        {
            // Ein defekter Claim darf keine Rechte verleihen — wie "keine Rolle" behandeln.
            return new List<VmRoleEntry>();
        }
    }
}
