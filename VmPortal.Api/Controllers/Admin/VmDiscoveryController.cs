using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VmPortal.Core.Data;
using VmPortal.Core.Data.Entities;
using VmPortal.Core.Interfaces;

namespace VmPortal.Api.Controllers.Admin;

/// <summary>
/// Read-only Vorschau des kompletten Hypervisor-Inventars (alle Hosts) mit Abgleich gegen die
/// Autorisierungs-DB, fürs Admin-Panel. Legt selbst nichts an - das tatsächliche Anlegen einer
/// bisher unbekannten VM passiert über <c>POST /api/admin/vm-groups/{groupId}/members</c>
/// (siehe VmGroupsController), das dieselben Host+Name/GUID-Daten entgegennimmt, die dieser
/// Endpunkt zurückgibt.
/// </summary>
[Route("api/admin/discover-vms")]
public class VmDiscoveryController : AdminControllerBase
{
    private readonly VmPortalDbContext _db;
    private readonly IVirtualizationProvider _virtualizationProvider;

    public VmDiscoveryController(
        VmPortalDbContext db,
        IVirtualizationProvider virtualizationProvider,
        IDbAuthorizationService authorizationService)
        : base(authorizationService)
    {
        _db = db;
        _virtualizationProvider = virtualizationProvider;
    }

    [HttpGet]
    public async Task<IActionResult> DiscoverVms()
    {
        // Bewusst das volle, ungefilterte Multi-Host-Inventar (Admin-Werkzeug, kein häufig
        // gepolltes Nutzer-Feature - siehe GetVmsAsync() ohne Parameter, derselbe Pfad wie für
        // Bootstrap-FullAdmin in VmController).
        var vms = await _virtualizationProvider.GetVmsAsync();

        // DB-Abgleich in zwei Abfragen für den gesamten Bestand statt einer Abfrage pro
        // gefundener VM (Lehre aus dem DB-first-Performance-Fix).
        var serverIdsByHost = await _db.VirtualServers
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Name, s => s.Id, StringComparer.OrdinalIgnoreCase);

        var recordsByKey = (await _db.VirtualMachines
                .AsNoTracking()
                .Include(vm => vm.Group)
                .ToListAsync())
            // (ServerId, Name) ist nicht per DB-Constraint erzwungen eindeutig - ToLookup
            // statt ToDictionary, damit ein theoretischer Duplikatfall nicht crasht.
            .ToLookup(vm => (vm.ServerId, Name: vm.Name.ToLowerInvariant()));

        var result = vms.Select(vm =>
        {
            VirtualMachineRecord? record = null;
            if (serverIdsByHost.TryGetValue(vm.HostName, out var serverId))
                record = recordsByKey[(serverId, vm.Name.ToLowerInvariant())].FirstOrDefault();

            return new DiscoveredVmDto(
                vm.HostName,
                vm.Name,
                string.IsNullOrEmpty(vm.VmGuid) ? null : vm.VmGuid,
                vm.Status.ToString(),
                ExistsInDb: record is not null,
                VirtualMachineRecordId: record?.Id,
                GroupId: record?.GroupId,
                GroupName: record?.Group?.Name);
        }).ToList();

        return Ok(result);
    }
}

public record DiscoveredVmDto(
    string HostName,
    string VmName,
    string? VmGuid,
    string Status,
    bool ExistsInDb,
    int? VirtualMachineRecordId,
    int? GroupId,
    string? GroupName);
