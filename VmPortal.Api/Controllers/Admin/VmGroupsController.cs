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

    [HttpGet("{groupId:int}/members")]
    public async Task<IActionResult> GetMembers(int groupId)
    {
        if (!await _db.VirtualMachineGroups.AnyAsync(g => g.Id == groupId))
            return NotFound(new { message = "VM-Gruppe nicht gefunden" });

        var members = await _db.VirtualMachines
            .AsNoTracking()
            .Where(vm => vm.GroupId == groupId)
            .Select(vm => new VmGroupMemberDto(vm.Id, vm.Server.Name, vm.Name, vm.VmGuid))
            .ToListAsync();

        return Ok(members);
    }

    /// <summary>
    /// Fügt eine oder mehrere VMs der Gruppe hinzu (Host + VM-Name, optional VM-GUID - z. B.
    /// direkt mit den Treffern aus <c>GET /api/admin/discover-vms</c>). Legt einen
    /// <see cref="VirtualMachineRecord"/> neu an, falls für (Host, Name) noch keiner existiert,
    /// statt einen Fehler zu werfen - VM-Discovery liefert bewusst nur Vorschaudaten, das
    /// tatsächliche Anlegen passiert hier. Server- und Bestandsabgleich laufen jeweils in
    /// einer Abfrage für den ganzen Batch, nicht pro VM einzeln.
    /// </summary>
    [HttpPost("{groupId:int}/members")]
    public async Task<IActionResult> AddMembers(int groupId, [FromBody] AddVmGroupMembersRequest request)
    {
        if (!await _db.VirtualMachineGroups.AnyAsync(g => g.Id == groupId))
            return NotFound(new { message = "VM-Gruppe nicht gefunden" });

        if (request.Vms is not { Count: > 0 })
            return BadRequest(new { message = "Mindestens eine VM ist erforderlich" });

        if (request.Vms.Any(v => string.IsNullOrWhiteSpace(v.HostName) || string.IsNullOrWhiteSpace(v.VmName)))
            return BadRequest(new { message = "HostName und VmName sind für jede VM erforderlich" });

        var hostNames = request.Vms.Select(v => v.HostName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var serverIdsByHost = await _db.VirtualServers
            .Where(s => hostNames.Contains(s.Name))
            .ToDictionaryAsync(s => s.Name, s => s.Id, StringComparer.OrdinalIgnoreCase);

        var unknownHosts = hostNames.Where(h => !serverIdsByHost.ContainsKey(h)).ToList();
        if (unknownHosts.Count > 0)
            return BadRequest(new { message = $"Unbekannte Hyper-V-Hosts: {string.Join(", ", unknownHosts)}" });

        var serverIds = serverIdsByHost.Values.Distinct().ToList();
        var vmNames = request.Vms.Select(v => v.VmName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var existingByKey = await _db.VirtualMachines
            .Where(vm => serverIds.Contains(vm.ServerId) && vmNames.Contains(vm.Name))
            .ToDictionaryAsync(vm => (vm.ServerId, vm.Name.ToLowerInvariant()));

        var created = 0;
        var updated = 0;

        foreach (var item in request.Vms)
        {
            var serverId = serverIdsByHost[item.HostName];
            var key = (serverId, item.VmName.ToLowerInvariant());

            if (existingByKey.TryGetValue(key, out var record))
            {
                record.GroupId = groupId;
                if (string.IsNullOrEmpty(record.VmGuid) && !string.IsNullOrEmpty(item.VmGuid))
                    record.VmGuid = item.VmGuid;
                updated++;
            }
            else
            {
                var newRecord = new VirtualMachineRecord
                {
                    ServerId = serverId,
                    Name = item.VmName,
                    VmGuid = item.VmGuid,
                    GroupId = groupId
                };
                _db.VirtualMachines.Add(newRecord);
                existingByKey[key] = newRecord; // Duplikate innerhalb desselben Requests abfangen
                created++;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new AddVmGroupMembersResult(created, updated));
    }

    /// <summary>
    /// Entfernt eine VM aus der Gruppe (GroupId -> null, secure-by-default), löscht dabei aber
    /// nicht den VirtualMachineRecord selbst - dieselbe Semantik wie beim Löschen einer
    /// gesamten Gruppe (siehe DeleteGroup oben).
    /// </summary>
    [HttpDelete("{groupId:int}/members/{memberId:int}")]
    public async Task<IActionResult> RemoveMember(int groupId, int memberId)
    {
        var record = await _db.VirtualMachines.FirstOrDefaultAsync(vm => vm.Id == memberId && vm.GroupId == groupId);
        if (record is null)
            return NotFound(new { message = "VM ist kein Mitglied dieser Gruppe" });

        record.GroupId = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record VmGroupDto(int Id, string Name, int VirtualMachineCount);

public record VmGroupDetailDto(int Id, string Name, IReadOnlyList<string> VirtualMachines);

public record VmGroupRequest(string Name);

public record VmGroupMemberDto(int Id, string HostName, string VmName, string? VmGuid);

public record AddVmGroupMemberRequest(string HostName, string VmName, string? VmGuid);

public record AddVmGroupMembersRequest(List<AddVmGroupMemberRequest> Vms);

public record AddVmGroupMembersResult(int Created, int Updated);
