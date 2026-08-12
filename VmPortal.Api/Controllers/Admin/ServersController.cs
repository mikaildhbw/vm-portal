using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VmPortal.Core.Data;
using VmPortal.Core.Data.Entities;
using VmPortal.Core.Interfaces;

namespace VmPortal.Api.Controllers.Admin;

[Route("api/admin/servers")]
public class ServersController : AdminControllerBase
{
    private readonly VmPortalDbContext _db;

    public ServersController(VmPortalDbContext db, IDbAuthorizationService authorizationService)
        : base(authorizationService)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetServers()
    {
        var servers = await _db.VirtualServers.AsNoTracking().ToListAsync();
        return Ok(servers);
    }

    [HttpPost]
    public async Task<IActionResult> CreateServer([FromBody] CreateServerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Address und Name sind erforderlich" });

        if (await _db.VirtualServers.AnyAsync(s => s.Address == request.Address))
            return BadRequest(new { message = $"Server '{request.Address}' existiert bereits" });

        var server = new VirtualServer
        {
            Address = request.Address,
            Platform = request.Platform,
            Name = request.Name
        };

        _db.VirtualServers.Add(server);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, server);
    }
}

public record CreateServerRequest(string Address, string Platform, string Name);
