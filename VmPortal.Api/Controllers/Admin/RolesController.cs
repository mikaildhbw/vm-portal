using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VmPortal.Core.Data;
using VmPortal.Core.Data.Entities;
using VmPortal.Core.Interfaces;

namespace VmPortal.Api.Controllers.Admin;

[Route("api/admin/roles")]
public class RolesController : AdminControllerBase
{
    private readonly VmPortalDbContext _db;

    public RolesController(VmPortalDbContext db, IDbAuthorizationService authorizationService)
        : base(authorizationService)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .Include(r => r.RoleActions).ThenInclude(ra => ra.Action)
            .OrderBy(r => r.Level)
            .ToListAsync();

        return Ok(roles.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name ist erforderlich" });

        if (await _db.Roles.AnyAsync(r => r.Name == request.Name))
            return BadRequest(new { message = $"Rolle '{request.Name}' existiert bereits" });

        var actionNames = request.Actions;
        if (actionNames is null && request.CloneFromRoleId is { } cloneFromRoleId)
        {
            actionNames = await _db.RoleActions
                .Where(ra => ra.RoleId == cloneFromRoleId)
                .Select(ra => ra.Action.Name)
                .ToListAsync();
        }

        var (actions, unknown) = await ResolveActionsAsync(actionNames ?? new List<string>());
        if (unknown.Count > 0)
            return BadRequest(new { message = $"Unbekannte Aktionen: {string.Join(", ", unknown)}" });

        var role = new Role
        {
            Name = request.Name,
            Level = request.Level,
            IsSystemRole = false,
            RoleActions = actions.Select(a => new RoleAction { Action = a }).ToList()
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, ToDto(role));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateRoleActions(int id, [FromBody] UpdateRoleActionsRequest request)
    {
        var role = await _db.Roles
            .Include(r => r.RoleActions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role is null)
            return NotFound(new { message = "Rolle nicht gefunden" });

        if (role.IsSystemRole)
            return BadRequest(new { message = "System-Rollen können nicht bearbeitet werden" });

        var (actions, unknown) = await ResolveActionsAsync(request.Actions);
        if (unknown.Count > 0)
            return BadRequest(new { message = $"Unbekannte Aktionen: {string.Join(", ", unknown)}" });

        _db.RoleActions.RemoveRange(role.RoleActions);
        role.RoleActions = actions.Select(a => new RoleAction { RoleId = role.Id, ActionId = a.Id }).ToList();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRole(int id)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role is null)
            return NotFound(new { message = "Rolle nicht gefunden" });

        if (role.IsSystemRole)
            return BadRequest(new { message = "System-Rollen können nicht gelöscht werden" });

        if (await _db.GroupPermissions.AnyAsync(gp => gp.RoleId == id))
            return BadRequest(new { message = "Rolle wird noch in aktiven Zuordnungen verwendet" });

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<(List<VmActionEntity> Found, List<string> Unknown)> ResolveActionsAsync(IReadOnlyCollection<string> actionNames)
    {
        var distinctNames = actionNames.Distinct().ToList();
        var found = await _db.VMActions.Where(a => distinctNames.Contains(a.Name)).ToListAsync();
        var unknown = distinctNames.Except(found.Select(a => a.Name)).ToList();
        return (found, unknown);
    }

    private static RoleDto ToDto(Role role) => new(
        role.Id,
        role.Name,
        role.IsSystemRole,
        role.Level,
        role.RoleActions.Select(ra => ra.Action.Name).OrderBy(name => name).ToList());
}

public record RoleDto(int Id, string Name, bool IsSystemRole, int Level, IReadOnlyList<string> Actions);

public record CreateRoleRequest(string Name, int Level, List<string>? Actions, int? CloneFromRoleId);

public record UpdateRoleActionsRequest(List<string> Actions);
