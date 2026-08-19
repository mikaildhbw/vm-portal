namespace VmPortal.Core.Interfaces;

/// <summary>
/// Durchsucht die konfigurierte AD-Gruppen (nicht Benutzer) fürs Admin-Panel (Rechtevergabe
/// über AD-Gruppe × VM-Gruppe × Rolle) - Gegenstück zur nutzerbezogenen Gruppenauflösung
/// beim Login (<see cref="IAuthService"/>), die nur die Gruppen des jeweils eingeloggten
/// Nutzers liefert, nicht den gesamten AD-Gruppenbestand.
/// </summary>
public interface IAdGroupSearchService
{
    /// <summary>
    /// Liefert Gruppennamen (CN), optional gefiltert per Teilstring-Suche auf den Namen.
    /// <paramref name="maxResults"/> begrenzt die Ergebnismenge server-seitig-freundlich
    /// (kein Vollabruf tausender AD-Gruppen) - <see cref="AdGroupSearchResult.Truncated"/>
    /// zeigt an, ob es weitere, nicht zurückgegebene Treffer gibt.
    /// </summary>
    Task<AdGroupSearchResult> SearchGroupsAsync(string? search, int maxResults);
}

public record AdGroupSearchResult(IReadOnlyList<string> Groups, bool Truncated);
