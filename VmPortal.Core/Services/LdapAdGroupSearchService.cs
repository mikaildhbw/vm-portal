using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;
using VmPortal.Core.Configuration;
using VmPortal.Core.Interfaces;

namespace VmPortal.Core.Services;

/// <summary>
/// Durchsucht AD-Gruppen fürs Admin-Panel. Anders als <see cref="LdapAuthService"/> (bindet
/// beim Login als der jeweilige Nutzer, dessen Passwort nur transient beim Login-Request
/// vorliegt) läuft diese Suche außerhalb eines Nutzer-Logins - sie braucht daher einen
/// eigenen Bind-Kontext: den optionalen Service-Account aus <see cref="LdapSettings"/>, sonst
/// einen anonymen Bind (RFC 4513 §5.1.2 - leerer DN, leeres Passwort), der auf restriktiv
/// konfigurierten ADs i. d. R. fehlschlägt. Nutzt denselben Verbindungsaufbau
/// (Novell.Directory.Ldap, kein SSL, ReferralFollowing aus) wie <see cref="LdapAuthService"/>,
/// kein zweiter paralleler LDAP-Client-Mechanismus.
/// </summary>
public class LdapAdGroupSearchService : IAdGroupSearchService
{
    private const string CnPrefix = "CN=";

    private readonly LdapSettings _settings;
    private readonly ILogger<LdapAdGroupSearchService> _logger;

    public LdapAdGroupSearchService(LdapSettings settings, ILogger<LdapAdGroupSearchService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<AdGroupSearchResult> SearchGroupsAsync(string? search, int maxResults)
    {
        using var conn = new LdapConnection { SecureSocketLayer = false };
        conn.Constraints.ReferralFollowing = false;

        try
        {
            await conn.ConnectAsync(_settings.Host, _settings.Port);
            await BindAsync(conn);

            var filter = string.IsNullOrWhiteSpace(search)
                ? "(objectClass=group)"
                : $"(&(objectClass=group)(cn=*{EscapeLdapFilterValue(search)}*))";

            var constraints = new LdapSearchConstraints
            {
                ReferralFollowing = false,
                // Defensiver Server-Cap gegen eine pathologisch große AD (Siemens-Produktion
                // kann sehr viele Gruppen haben) - die eigentliche Begrenzung auf maxResults
                // passiert client-seitig unten, damit wir sauber "truncated" statt einer
                // LdapException.SizeLimitExceeded erhalten.
                MaxResults = Math.Max(maxResults + 1, 200)
            };

            var searchResults = await conn.SearchAsync(
                _settings.BaseDn, LdapConnection.ScopeSub, filter, new[] { "cn" }, false, constraints);

            var groups = new List<string>();
            var truncated = false;

            await foreach (var entry in searchResults)
            {
                if (groups.Count >= maxResults)
                {
                    truncated = true;
                    break;
                }

                var cn = entry.GetAttributeSet().ContainsKey("cn")
                    ? entry.GetAttributeSet()["cn"].StringValue
                    : ExtractCommonName(entry.Dn);

                if (cn is not null)
                    groups.Add(cn);
            }

            groups.Sort(StringComparer.OrdinalIgnoreCase);
            return new AdGroupSearchResult(groups, truncated);
        }
        catch (LdapException ex)
        {
            _logger.LogError(ex, "AD-Gruppensuche fehlgeschlagen (Host {Host}, BaseDn {BaseDn})",
                _settings.Host, _settings.BaseDn);
            throw;
        }
    }

    private Task BindAsync(LdapConnection conn)
    {
        if (!string.IsNullOrEmpty(_settings.ServiceAccountUsername))
        {
            var domain = _settings.BaseDn.Replace("DC=", "", StringComparison.OrdinalIgnoreCase).Replace(",", ".");
            return conn.BindAsync($"{_settings.ServiceAccountUsername}@{domain}", _settings.ServiceAccountPassword);
        }

        _logger.LogWarning(
            "Kein Ldap:ServiceAccountUsername konfiguriert - versuche anonymen LDAP-Bind für " +
            "die AD-Gruppensuche. Schlägt das fehl, muss ein Service-Account hinterlegt werden.");
        return conn.BindAsync(string.Empty, string.Empty);
    }

    private static string? ExtractCommonName(string distinguishedName)
    {
        var firstRdn = distinguishedName.Split(',')[0].Trim();
        return firstRdn.StartsWith(CnPrefix, StringComparison.OrdinalIgnoreCase)
            ? firstRdn[CnPrefix.Length..]
            : null;
    }

    /// <summary>Escaped die für LDAP-Filter (RFC 4515) reservierten Zeichen, damit
    /// <paramref name="value"/> nicht als Filter-Syntax statt als Suchbegriff interpretiert
    /// werden kann (z. B. ein "*" im Suchbegriff dürfte sonst zum Wildcard werden).</summary>
    private static string EscapeLdapFilterValue(string value) => value
        .Replace("\\", "\\5c")
        .Replace("*", "\\2a")
        .Replace("(", "\\28")
        .Replace(")", "\\29")
        .Replace("\0", "\\00");
}
