using VmPortal.Core.Configuration;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Services;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Abschnitt 'Jwt' fehlt in appsettings.json");

builder.Services.AddControllers();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IVirtualizationProvider, DummyVirtualizationProvider>();
builder.Services.AddScoped<IAuthService>(sp =>
    new LdapAuthService(
        ldapHost: "192.168.122.196",
        baseDn: "DC=testumgebung,DC=local",
        tokenService: sp.GetRequiredService<ITokenService>(),
        ldapPort: 389
    ));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
