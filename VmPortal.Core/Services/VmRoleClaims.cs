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

    public static IReadOnlyDictionary<string, VmRole> Deserialize(string? claimValue)
    {
        var vmRoles = new Dictionary<string, VmRole>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(claimValue))
            return vmRoles;

        List<VmRoleEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<VmRoleEntry>>(claimValue);
        }
        catch (JsonException)
        {
            // Ein defekter Claim darf keine Rechte verleihen — wie "keine Rolle" behandeln.
            return vmRoles;
        }

        if (entries is null)
            return vmRoles;

        foreach (var entry in entries)
        {
            if (Enum.TryParse<VmRole>(entry.Role, ignoreCase: true, out var role))
                vmRoles[entry.Vm] = role;
        }

        return vmRoles;
    }
}
