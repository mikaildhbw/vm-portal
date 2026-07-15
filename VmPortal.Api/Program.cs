using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using VmPortal.Api.Constants;
using VmPortal.Api.Middleware;
using VmPortal.Core.Configuration;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Services;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Abschnitt 'Jwt' fehlt in appsettings.json");
var ldapSettings = builder.Configuration.GetSection("Ldap").Get<LdapSettings>()
    ?? throw new InvalidOperationException("Abschnitt 'Ldap' fehlt in appsettings.json");

builder.Services.AddControllers();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(ldapSettings);
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, LdapAuthService>();
RegisterVirtualizationProvider(builder);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
        };

        // Token kommt aus dem httpOnly Cookie, nicht aus dem Authorization Header
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AuthConstants.TokenCookieName, out var token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<VirtualizationExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Wählt die konkrete Hypervisor-Implementierung anhand der Konfiguration aus.
// So lässt sich der Portal-Kern ohne Codeänderung gegen einen anderen Provider betreiben
// (Dummy für lokale Entwicklung, HyperV in Produktion, konzeptionell auch Proxmox).
static void RegisterVirtualizationProvider(WebApplicationBuilder builder)
{
    var provider = builder.Configuration["Virtualization:Provider"] ?? "Dummy";

    if (string.Equals(provider, "HyperV", StringComparison.OrdinalIgnoreCase))
    {
        var hyperVSettings = builder.Configuration.GetSection("HyperV").Get<HyperVSettings>()
            ?? throw new InvalidOperationException("Abschnitt 'HyperV' fehlt in appsettings.json");
        builder.Services.AddSingleton(hyperVSettings);
        builder.Services.AddScoped<IVirtualizationProvider, HyperVProvider>();
        return;
    }

    builder.Services.AddScoped<IVirtualizationProvider, DummyVirtualizationProvider>();
}
