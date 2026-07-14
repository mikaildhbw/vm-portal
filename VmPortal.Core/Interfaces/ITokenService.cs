namespace VmPortal.Core.Interfaces;

public interface ITokenService
{
    string GenerateToken(string username, string role);
    bool ValidateToken(string token);
}
