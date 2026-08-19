using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VmPortal.Api.Constants;
using VmPortal.Api.Middleware;
using VmPortal.Core.Configuration;
using VmPortal.Core.Data;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Services;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Abschnitt 'Jwt' fehlt in appsettings.json");
var ldapSettings = builder.Configuration.GetSection("Ldap").Get<LdapSettings>()
    ?? throw new InvalidOperationException("Abschnitt 'Ldap' fehlt in appsettings.json");

const string FrontendCorsPolicy = "FrontendDev";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowCredentials()   // notwendig, damit das httpOnly-Cookie übertragen wird
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var testVmRoles = builder.Configuration.GetSection("TestVmRoles").Get<TestVmRolesSettings>()
    ?? new TestVmRolesSettings();
var testAdGroups = builder.Configuration.GetSection("TestAdGroups").Get<TestAdGroupsSettings>()
    ?? new TestAdGroupsSettings();
var authorizationSettings = builder.Configuration.GetSection("Authorization").Get<AuthorizationSettings>()
    ?? throw new InvalidOperationException("Abschnitt 'Authorization' fehlt in appsettings.json");

builder.Services.AddControllers();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(ldapSettings);
builder.Services.AddSingleton(testVmRoles);
builder.Services.AddSingleton(testAdGroups);
builder.Services.AddSingleton(authorizationSettings);
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
RegisterDatabase(builder);
RegisterAuthService(builder);
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
            },
            // Eigenes Log-Signal für "nicht authentifiziert" (401), damit dieser Fall in
            // den Logs klar von den DB-Autorisierungsverweigerungen (403, siehe
            // DbAuthorizationService/VmController) unterscheidbar ist.
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Authentication");
                logger.LogInformation(
                    "nicht authentifiziert: {Method} {Path} ({Reason})",
                    context.Request.Method, context.Request.Path,
                    context.AuthenticateFailure?.Message ?? "kein gültiges JWT-Cookie");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<VirtualizationExceptionMiddleware>();
app.UseHttpsRedirection();

// Statisches React-Frontend aus wwwroot ausliefern.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SPA-Fallback: alle Nicht-API-Routen liefern index.html, damit das clientseitige
// Routing (React Router) funktioniert.
app.MapFallbackToFile("index.html");

app.Run();

// Wählt den Auth-Dienst anhand der Konfiguration aus: "Ldap" (Default) authentifiziert
// gegen das AD, "Dummy" simuliert Testbenutzer und VM-Rollen aus "TestVmRoles" —
// analog zur Provider-Auswahl bei der Virtualisierung.
static void RegisterAuthService(WebApplicationBuilder builder)
{
    var provider = builder.Configuration["Auth:Provider"] ?? "Ldap";

    if (string.Equals(provider, "Dummy", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddScoped<IAuthService, DummyAuthService>();
        return;
    }

    builder.Services.AddScoped<IAuthService, LdapAuthService>();
}

// Registriert den SQLite-DbContext für die Autorisierungsschicht und legt das
// Zielverzeichnis der Datenbankdatei an, falls es noch nicht existiert (relevant für
// C:\VmPortal\data in Produktion, das beim ersten Deployment nicht existiert).
static void RegisterDatabase(WebApplicationBuilder builder)
{
    var connectionString = builder.Configuration.GetConnectionString("VmPortalDb")
        ?? throw new InvalidOperationException("ConnectionString 'VmPortalDb' fehlt in appsettings.json");

    var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
    var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
    if (!string.IsNullOrEmpty(directory))
        Directory.CreateDirectory(directory);

    builder.Services.AddDbContext<VmPortalDbContext>(options => options.UseSqlite(connectionString));
    builder.Services.AddScoped<IDbAuthorizationService, DbAuthorizationService>();
}

// Wählt die konkrete Hypervisor-Implementierung anhand der Konfiguration aus.
// So lässt sich der Portal-Kern ohne Codeänderung gegen einen anderen Provider betreiben
// (Dummy für lokale Entwicklung, HyperV in Produktion, konzeptionell auch Proxmox).
static void RegisterVirtualizationProvider(WebApplicationBuilder builder)
{
    var provider = builder.Configuration["Virtualization:Provider"] ?? "Dummy";

    if (string.Equals(provider, "HyperV", StringComparison.OrdinalIgnoreCase))
    {
        // Ohne "Virtualization:HyperV"-Abschnitt (z. B. bestehende Testumgebungs-Config)
        // bleibt HyperVSettings.Mode auf Local - identisch zum bisherigen, rein lokalen
        // Verhalten auf dem Hyper-V-Host selbst, keine Verbindungskonfiguration nötig.
        var hyperVSettings = builder.Configuration.GetSection("Virtualization:HyperV").Get<HyperVSettings>()
            ?? new HyperVSettings();
        builder.Services.AddSingleton(hyperVSettings);

        // Singleton statt Scoped: im Remote-Modus hält der Provider pro Host einen
        // WinRM-RunspacePool, der über die gesamte Prozesslaufzeit wiederverwendet werden
        // soll (Verbindungsaufbau ist bei WinRM spürbar teurer als lokal).
        builder.Services.AddSingleton<IVirtualizationProvider, HyperVProvider>();
        return;
    }

    builder.Services.AddScoped<IVirtualizationProvider, DummyVirtualizationProvider>();
}
