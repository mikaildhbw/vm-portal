using VmPortal.Core.Interfaces;

namespace VmPortal.Core.Services;

public class DummyAuthService : IAuthService
{
    public Task<AuthResult> LoginAsync(string username, string password)
    {
        if (username == "mugur" && password == "Test1234!")
            return Task.FromResult(new AuthResult(true, "dummy-token-123", null));

        return Task.FromResult(new AuthResult(false, null, "Ungültige Zugangsdaten"));
    }

    public Task<bool> ValidateTokenAsync(string token) =>
        Task.FromResult(token == "dummy-token-123");
}
