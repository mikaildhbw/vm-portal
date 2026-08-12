using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VmPortal.Core.Data;
using VmPortal.Core.Data.Entities;
using VmPortal.Core.Interfaces;

namespace VmPortal.Api.Controllers.Admin;

[Route("api/admin/vm-groups")]
public class VmGroupsController : AdminControllerBase
{
    private readonly VmPortalDbContext _db;

    public VmGroupsController(VmPortalDbContext db, IDbAuthorizationService authorizationService)
        : base(authorizationService)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await _db.VirtualMachineGroups
            .AsNoTracking()
            .Select(g => new VmGroupDto(g.Id, g.Name, g.VirtualMachines.Count))
            .ToListAsync();

        return Ok(groups);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetGroup(int id)
    {
        var group = await _db.VirtualMachineGroups
            .AsNoTracking()
            .Include(g => g.VirtualMachines)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null)
            return NotFound(new { message = "VM-Gruppe nicht gefunden" });

        return Ok(new VmGroupDetailDto(
            group.Id, group.Name, group.VirtualMachines.Select(vm => vm.Name).ToList()));
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] VmGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name ist erforderlich" });

        if (await _db.VirtualMachineGroups.AnyAsync(g => g.Name == request.Name))
            return BadRequest(new { message = $"VM-Gruppe '{request.Name}' existiert bereits" });

        var group = new VirtualMachineGroup { Name = request.Name };
        _db.VirtualMachineGroups.Add(group);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new VmGroupDto(group.Id, group.Name, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> RenameGroup(int id, [FromBody] VmGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name ist erforderlich" });

        var group = await _db.VirtualMachineGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null)
            return NotFound(new { message = "VM-Gruppe nicht gefunden" });

        if (await _db.VirtualMachineGroups.AnyAsync(g => g.Id != id && g.Name == request.Name))
            return BadRequest(new { message = $"VM-Gruppe '{request.Name}' existiert bereits" });

        group.Name = request.Name;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        var group = await _db.VirtualMachineGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null)
            return NotFound(new { message = "VM-Gruppe nicht gefunden" });

        // VMs dieser Gruppe werden secure-by-default gruppenlos (GroupId -> null, siehe
        // VmPortalDbContext.OnModelCreating DeleteBehavior.SetNull), nicht mitgelöscht.
        _db.VirtualMachineGroups.Remove(group);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record VmGroupDto(int Id, string Name, int VirtualMachineCount);

public record VmGroupDetailDto(int Id, string Name, IReadOnlyList<string> VirtualMachines);

public record VmGroupRequest(string Name);
