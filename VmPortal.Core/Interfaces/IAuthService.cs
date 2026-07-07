namespace VmPortal.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task<bool> ValidateTokenAsync(string token);
}

public record AuthResult(bool Success, string? Token, string? ErrorMessage);
