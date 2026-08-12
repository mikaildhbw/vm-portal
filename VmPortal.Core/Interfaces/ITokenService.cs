using VmPortal.Core.Models;

namespace VmPortal.Core.Interfaces;

public interface ITokenService
{
    string GenerateToken(
        string username,
        string role,
        IReadOnlyDictionary<string, VmRole>? vmRoles = null,
        IReadOnlyCollection<string>? adGroups = null);

    bool ValidateToken(string token);
}
