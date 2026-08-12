using System.Security.Claims;

namespace VmPortal.Core.Services;

/// <summary>
/// Trägt die rohen AD-Gruppennamen eines Nutzers (memberOf-CNs) als eigenen Claim im JWT -
/// getrennt vom "vmroles"-Claim (<see cref="VmRoleClaims"/>), der weiterhin unverändert für
/// die bestehende VM-Name-zu-Rolle-Zuordnung genutzt wird. DbAuthorizationService matcht
/// diese Gruppennamen gegen UserGroups.Name.
/// </summary>
public static class AdGroupClaims
{
    public const string ClaimType = "adgroups";

    public static IReadOnlyCollection<string> Deserialize(IEnumerable<string?> claimValues) =>
        claimValues.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToList();

    public static IReadOnlyCollection<string> FromPrincipal(ClaimsPrincipal user) =>
        Deserialize(user.FindAll(ClaimType).Select(claim => claim.Value));
}
