using System.Text.RegularExpressions;
using Novell.Directory.Ldap;
using VmPortal.Core.Configuration;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Models;

namespace VmPortal.Core.Services;

public class LdapAuthService : IAuthService
{
    // Rollengruppen folgen dem Schema "VM-{VmName}-{Rolle}" (z. B. "VM-Mikail-PowerUser");
    // nach Abtrennen des Rollen-Suffixes bleibt der vollständige VM-Name ("VM-Mikail") übrig.
    private static readonly Regex VmRoleGroupPattern = new(
        "^(?<vm>VM-.+)-(?<role>Viewer|Operator|PowerUser|Admin|FullAdmin)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string CnPrefix = "CN=";

    private readonly string _ldapHost;
    private readonly int _ldapPort;
    private readonly string _baseDn;
    private readonly ITokenService _tokenService;

    public LdapAuthService(LdapSettings settings, ITokenService tokenService)
    {
        _ldapHost = settings.Host;
        _ldapPort = settings.Port;
        _baseDn = settings.BaseDn;
        _tokenService = tokenService;
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        try
        {
            using var conn = new LdapConnection { SecureSocketLayer = false };
            conn.Constraints.ReferralFollowing = false;
            await conn.ConnectAsync(_ldapHost, _ldapPort);
            await conn.BindAsync($"{username}@{_baseDn.Replace("DC=", "").Replace(",", ".")}", password);

            var groupNames = await LoadGroupNamesAsync(conn, username);
            var role = groupNames.Contains("VM-Portal-Benutzer") ? "VMUser" : "User";
            var vmRoles = ExtractVmRoles(groupNames);

            return new AuthResult(true, _tokenService.GenerateToken(username, role, vmRoles, groupNames), null);
        }
        catch (LdapException ex)
        {
            return new AuthResult(false, null, $"Login fehlgeschlagen: {ex.Message}");
        }
    }

    public Task<bool> ValidateTokenAsync(string token) =>
        Task.FromResult(_tokenService.ValidateToken(token));

    private async Task<List<string>> LoadGroupNamesAsync(LdapConnection conn, string username)
    {
        var constraints = new LdapSearchConstraints { ReferralFollowing = false };
        var search = await conn.SearchAsync(
            _baseDn,
            LdapConnection.ScopeSub,
            $"(sAMAccountName={username})",
            new[] { "memberOf" },
            false,
            constraints
        );

        var groupNames = new List<string>();
        try
        {
            await foreach (var entry in search)
            {
                var attrSet = entry.GetAttributeSet();
                if (!attrSet.ContainsKey("memberOf"))
                    continue;

                foreach (var dn in attrSet["memberOf"].StringValueArray)
                {
                    var cn = ExtractCommonName(dn);
                    if (cn is not null)
                        groupNames.Add(cn);
                }
            }
        }
        catch (LdapReferralException)
        {
            // Referrals ignorieren
        }

        return groupNames;
    }

    /// <summary>
    /// Baut aus allen Rollengruppen-Mitgliedschaften die VM->Rolle-Zuordnung des Nutzers.
    /// Ist ein Nutzer in mehreren Rollengruppen derselben VM, gilt die höchste Rolle.
    /// </summary>
    private static Dictionary<string, VmRole> ExtractVmRoles(IEnumerable<string> groupNames)
    {
        var vmRoles = new Dictionary<string, VmRole>(StringComparer.OrdinalIgnoreCase);

        foreach (var groupName in groupNames)
        {
            var match = VmRoleGroupPattern.Match(groupName);
            if (!match.Success)
                continue;

            var vmName = match.Groups["vm"].Value;
            var role = Enum.Parse<VmRole>(match.Groups["role"].Value, ignoreCase: true);

            if (!vmRoles.TryGetValue(vmName, out var existingRole) || role > existingRole)
                vmRoles[vmName] = role;
        }

        return vmRoles;
    }

    private static string? ExtractCommonName(string distinguishedName)
    {
        var firstRdn = distinguishedName.Split(',')[0].Trim();
        return firstRdn.StartsWith(CnPrefix, StringComparison.OrdinalIgnoreCase)
            ? firstRdn[CnPrefix.Length..]
            : null;
    }
}
