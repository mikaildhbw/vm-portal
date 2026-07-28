using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VmPortal.Core.Configuration;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Models;

namespace VmPortal.Core.Services;

public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(JwtSettings settings)
    {
        _settings = settings;
    }

    public string GenerateToken(string username, string role, IReadOnlyDictionary<string, VmRole>? vmRoles = null)
    {
        // "role" bleibt als globaler Legacy-Claim erhalten; die VM-spezifische
        // Autorisierung läuft über den "vmroles"-Claim.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, username),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (vmRoles is { Count: > 0 })
            claims.Add(new Claim(VmRoleClaims.ClaimType, VmRoleClaims.Serialize(vmRoles), JsonClaimValueTypes.Json));

        var credentials = new SigningCredentials(CreateSigningKey(), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_settings.ExpiryHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool ValidateToken(string token)
    {
        try
        {
            new JwtSecurityTokenHandler().ValidateToken(token, CreateValidationParameters(), out _);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = _settings.Issuer,
        ValidAudience = _settings.Audience,
        IssuerSigningKey = CreateSigningKey()
    };

    private SymmetricSecurityKey CreateSigningKey() =>
        new(Encoding.UTF8.GetBytes(_settings.Secret));
}
