using VmPortal.Core.Configuration;
using VmPortal.Core.Interfaces;

namespace VmPortal.Core.Services;

/// <summary>
/// Simuliert die AD-Gruppensuche für die lokale Entwicklung ohne echtes AD - Pendant zu
/// <see cref="DummyAuthService"/>. Quelle sind die aus <see cref="TestAdGroupsSettings"/>
/// bekannten Gruppennamen, damit sich das Admin-Panel lokal gegen dieselben Testgruppen
/// testen lässt, die auch für den Login simuliert werden.
/// </summary>
public class DummyAdGroupSearchService : IAdGroupSearchService
{
    private readonly IReadOnlyList<string> _groups;

    public DummyAdGroupSearchService(TestAdGroupsSettings settings)
    {
        _groups = settings.Users.Values
            .SelectMany(groups => groups)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<AdGroupSearchResult> SearchGroupsAsync(string? search, int maxResults)
    {
        var matches = string.IsNullOrWhiteSpace(search)
            ? _groups
            : _groups.Where(g => g.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        var truncated = matches.Count > maxResults;
        var result = matches.Take(maxResults).ToList();

        return Task.FromResult(new AdGroupSearchResult(result, truncated));
    }
}
