using VmPortal.Core.Configuration;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Models;

namespace VmPortal.Core.Services;

/// <summary>
/// Auth-Platzhalter für die lokale Entwicklung ohne echtes AD. Stellt dieselben
/// Testbenutzer wie die Testumgebung bereit und liest die VM->Rolle-Zuordnungen
/// aus dem Konfigurationsabschnitt "TestVmRoles" statt aus AD-Gruppen. Erzeugt
/// ein echtes JWT, damit die restliche Auth-Pipeline unverändert funktioniert.
/// </summary>
public class DummyAuthService : IAuthService
{
    private const string TestPassword = "Test1234!";
    private static readonly string[] TestUsers = { "mugur", "jburath" };

    private readonly ITokenService _tokenService;
    private readonly TestVmRolesSettings _testVmRoles;

    public DummyAuthService(ITokenService tokenService, TestVmRolesSettings testVmRoles)
    {
        _tokenService = tokenService;
        _testVmRoles = testVmRoles;
    }

    public Task<AuthResult> LoginAsync(string username, string password)
    {
        if (!TestUsers.Contains(username) || password != TestPassword)
            return Task.FromResult(new AuthResult(false, null, "Ungültige Zugangsdaten"));

        var token = _tokenService.GenerateToken(username, "VMUser", ResolveVmRoles(username));
        return Task.FromResult(new AuthResult(true, token, null));
    }

    public Task<bool> ValidateTokenAsync(string token) =>
        Task.FromResult(_tokenService.ValidateToken(token));

    private Dictionary<string, VmRole> ResolveVmRoles(string username)
    {
        var vmRoles = new Dictionary<string, VmRole>(StringComparer.OrdinalIgnoreCase);
        if (!_testVmRoles.Users.TryGetValue(username, out var assignments))
            return vmRoles;

        foreach (var (vmName, roleName) in assignments)
        {
            if (Enum.TryParse<VmRole>(roleName, ignoreCase: true, out var role))
                vmRoles[vmName] = role;
        }

        return vmRoles;
    }
}
