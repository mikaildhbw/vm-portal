using Microsoft.AspNetCore.Mvc;
using Novell.Directory.Ldap;
using VmPortal.Core.Interfaces;

namespace VmPortal.Api.Controllers.Admin;

/// <summary>
/// AD-Gruppensuche fürs Admin-Panel (Rechtevergabe über AD-Gruppe × VM-Gruppe × Rolle) -
/// unabhängig vom nutzerbezogenen "adgroups"-Claim, der nur die Gruppen des jeweils
/// eingeloggten Nutzers liefert.
/// </summary>
[Route("api/admin/ad-groups")]
public class AdGroupsController : AdminControllerBase
{
    // Ohne Suchbegriff bewusst klein gehalten - die produktive AD-Struktur kann sehr viele
    // Gruppen enthalten, ein Vollabruf skaliert nicht. Mit Suchbegriff bereits eingeschränkt,
    // daher etwas großzügiger.
    private const int DefaultMaxResults = 50;
    private const int SearchMaxResults = 100;

    private readonly IAdGroupSearchService _adGroupSearchService;

    public AdGroupsController(IAdGroupSearchService adGroupSearchService, IDbAuthorizationService authorizationService)
        : base(authorizationService)
    {
        _adGroupSearchService = adGroupSearchService;
    }

    [HttpGet]
    public async Task<IActionResult> SearchGroups([FromQuery] string? search)
    {
        var maxResults = string.IsNullOrWhiteSpace(search) ? DefaultMaxResults : SearchMaxResults;

        try
        {
            var result = await _adGroupSearchService.SearchGroupsAsync(search, maxResults);
            return Ok(new AdGroupSearchResponse(
                result.Groups,
                result.Truncated,
                result.Truncated ? "Weitere Treffer vorhanden - Suchbegriff eingrenzen." : null));
        }
        catch (LdapException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = $"AD-Gruppensuche fehlgeschlagen: {ex.Message}" });
        }
    }
}

public record AdGroupSearchResponse(IReadOnlyList<string> Groups, bool Truncated, string? Hint);
