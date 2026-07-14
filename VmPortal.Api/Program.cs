using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using VmPortal.Api.Constants;
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

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
