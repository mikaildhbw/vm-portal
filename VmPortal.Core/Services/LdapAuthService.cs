using Novell.Directory.Ldap;
using VmPortal.Core.Configuration;
using VmPortal.Core.Interfaces;

namespace VmPortal.Core.Services;

public class LdapAuthService : IAuthService
{
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

            var constraints = new LdapSearchConstraints { ReferralFollowing = false };
            var search = await conn.SearchAsync(
                _baseDn,
                LdapConnection.ScopeSub,
                $"(sAMAccountName={username})",
                new[] { "memberOf" },
                false,
                constraints
            );

            string role = "User";
            try
            {
                await foreach (var entry in search)
                {
                    var attrSet = entry.GetAttributeSet();
                    if (attrSet.ContainsKey("memberOf"))
                    {
                        var memberOfAttr = attrSet["memberOf"];
                        foreach (var val in memberOfAttr.StringValueArray)
                        {
                            if (val.Contains("VM-Portal-Benutzer"))
                            {
                                role = "VMUser";
                                break;
                            }
                        }
                    }
                }
            }
            catch (LdapReferralException)
            {
                // Referrals ignorieren
            }

            return new AuthResult(true, _tokenService.GenerateToken(username, role), null);
        }
        catch (LdapException ex)
        {
            return new AuthResult(false, null, $"Login fehlgeschlagen: {ex.Message}");
        }
    }

    public Task<bool> ValidateTokenAsync(string token) =>
        Task.FromResult(_tokenService.ValidateToken(token));
}
