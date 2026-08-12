using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VmPortal.Core.Data;
using VmPortal.Core.Data.Entities;
using VmPortal.Core.Interfaces;

namespace VmPortal.Api.Controllers.Admin;

[Route("api/admin/permissions")]
public class PermissionsController : AdminControllerBase
{
    private readonly VmPortalDbContext _db;

    public PermissionsController(VmPortalDbContext db, IDbAuthorizationService authorizationService)
        : base(authorizationService)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _db.GroupPermissions
            .AsNoTracking()
            .Include(gp => gp.VmGroup)
            .Include(gp => gp.UserGroup)
            .Include(gp => gp.Role)
            .Select(gp => new GroupPermissionDto(
                gp.Id, gp.VmGroupId, gp.VmGroup.Name, gp.UserGroupId, gp.UserGroup.Name, gp.RoleId, gp.Role.Name))
            .ToListAsync();

        return Ok(permissions);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionRequest request)
    {
        if (!await _db.VirtualMachineGroups.AnyAsync(g => g.Id == request.VmGroupId))
            return BadRequest(new { message = "VM-Gruppe nicht gefunden" });

        if (!await _db.UserGroups.AnyAsync(ug => ug.Id == request.UserGroupId))
            return BadRequest(new { message = "Benutzergruppe nicht gefunden" });

        if (!await _db.Roles.AnyAsync(r => r.Id == request.RoleId))
            return BadRequest(new { message = "Rolle nicht gefunden" });

        var exists = await _db.GroupPermissions.AnyAsync(gp =>
            gp.VmGroupId == request.VmGroupId && gp.UserGroupId == request.UserGroupId && gp.RoleId == request.RoleId);
        if (exists)
            return BadRequest(new { message = "Diese Zuordnung existiert bereits" });

        var permission = new GroupPermission
        {
            VmGroupId = request.VmGroupId,
            UserGroupId = request.UserGroupId,
            RoleId = request.RoleId
        };

        _db.GroupPermissions.Add(permission);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new { permission.Id });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePermission(int id)
    {
        var permission = await _db.GroupPermissions.FirstOrDefaultAsync(gp => gp.Id == id);
        if (permission is null)
            return NotFound(new { message = "Zuordnung nicht gefunden" });

        _db.GroupPermissions.Remove(permission);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record GroupPermissionDto(
    int Id, int VmGroupId, string VmGroupName, int UserGroupId, string UserGroupName, int RoleId, string RoleName);

public record CreatePermissionRequest(int VmGroupId, int UserGroupId, int RoleId);
