using VmPortal.Core.Interfaces;
using VmPortal.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<IVirtualizationProvider, DummyVirtualizationProvider>();
builder.Services.AddScoped<IAuthService, DummyAuthService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
