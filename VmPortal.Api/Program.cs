using VmPortal.Core.Interfaces;
using VmPortal.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<IVirtualizationProvider, DummyVirtualizationProvider>();
builder.Services.AddScoped<IAuthService>(_ =>
    new LdapAuthService(
        ldapHost: "192.168.122.196",
        baseDn: "DC=testumgebung,DC=local",
        ldapPort: 389
    ));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
