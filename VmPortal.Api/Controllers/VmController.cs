using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VmPortal.Core.Interfaces;
using VmPortal.Core.Models;
using VmPortal.Core.Services;

namespace VmPortal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VmController : ControllerBase
{
    private readonly IVirtualizationProvider _virtualizationProvider;
    private readonly ILogger<VmController> _logger;

    public VmController(IVirtualizationProvider virtualizationProvider, ILogger<VmController> logger)
    {
        _virtualizationProvider = virtualizationProvider;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetVms()
    {
        var vmRoles = GetVmRolesFromToken();
        var vms = await _virtualizationProvider.GetVmsAsync();

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug(
                "GET /api/vm: vmroles-Claims [{RawClaims}] -> geparst [{ParsedRoles}], Provider-VMs [{VmNames}]",
                string.Join(" | ", User.FindAll(VmRoleClaims.ClaimType).Select(c => c.Value)),
                string.Join(", ", vmRoles.Select(r => $"{r.Key}={r.Value}")),
                string.Join(", ", vms.Select(vm => vm.Name)));

        return Ok(vms.Where(vm =>
            vmRoles.TryGetValue(vm.Name, out var role) &&
            RolePermissions.IsAllowed(role, VmAction.ViewStatus)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetVm(string id)
    {
        var (vm, error) = await AuthorizeVmActionAsync(id, VmAction.ViewDetails);
        if (error is not null)
            return error;

        return Ok(vm);
    }

    [HttpGet("{id}/metering")]
    public async Task<IActionResult> GetMetering(string id)
    {
        var (_, error) = await AuthorizeVmActionAsync(id, VmAction.ViewMetering);
        if (error is not null)
            return error;

        var metering = await _virtualizationProvider.GetMeteringAsync(id);
        return Ok(metering);
    }

    [HttpPost("{id}/start")]
    public Task<IActionResult> StartVm(string id) =>
        ExecuteVmActionAsync(id, VmAction.Start, () => _virtualizationProvider.StartVmAsync(id));

    [HttpPost("{id}/stop")]
    public Task<IActionResult> StopVm(string id) =>
        ExecuteVmActionAsync(id, VmAction.Stop, () => _virtualizationProvider.StopVmAsync(id));

    [HttpPost("{id}/pause")]
    public Task<IActionResult> PauseVm(string id) =>
        ExecuteVmActionAsync(id, VmAction.Pause, () => _virtualizationProvider.PauseVmAsync(id));

    [HttpPost("{id}/resume")]
    public Task<IActionResult> ResumeVm(string id) =>
        ExecuteVmActionAsync(id, VmAction.Resume, () => _virtualizationProvider.ResumeVmAsync(id));

    [HttpPost("{id}/save-state")]
    public Task<IActionResult> SaveState(string id) =>
        ExecuteVmActionAsync(id, VmAction.SaveState, () => _virtualizationProvider.SaveStateAsync(id));

    [HttpPost("{id}/reset")]
    public Task<IActionResult> ResetVm(string id) =>
        ExecuteVmActionAsync(id, VmAction.Reset, () => _virtualizationProvider.ResetVmAsync(id));

    [HttpPost("{id}/snapshot")]
    public Task<IActionResult> CreateSnapshot(string id, [FromBody] string snapshotName) =>
        ExecuteVmActionAsync(id, VmAction.SnapshotCreate,
            () => _virtualizationProvider.CreateSnapshotAsync(id, snapshotName));

    [HttpPost("{id}/snapshot/apply")]
    public Task<IActionResult> ApplySnapshot(string id, [FromBody] string snapshotName) =>
        ExecuteVmActionAsync(id, VmAction.SnapshotApply,
            () => _virtualizationProvider.ApplySnapshotAsync(id, snapshotName));

    [HttpDelete("{id}/snapshot/{snapshotName}")]
    public Task<IActionResult> DeleteSnapshot(string id, string snapshotName) =>
        ExecuteVmActionAsync(id, VmAction.SnapshotDelete,
            () => _virtualizationProvider.DeleteSnapshotAsync(id, snapshotName));

    [HttpPost("{id}/console")]
    public async Task<IActionResult> ConnectConsole(string id)
    {
        var (_, error) = await AuthorizeVmActionAsync(id, VmAction.ConsoleConnect);
        if (error is not null)
            return error;

        var connection = await _virtualizationProvider.GetConsoleConnectionAsync(id);
        return Ok(new { connection });
    }

    [HttpPost("{id}/resize-ram")]
    public Task<IActionResult> ResizeRam(string id, [FromBody] int ramMb) =>
        ExecuteVmActionAsync(id, VmAction.ResizeRam, () => _virtualizationProvider.ResizeRamAsync(id, ramMb));

    [HttpPost("{id}/resize-cpu")]
    public Task<IActionResult> ResizeCpu(string id, [FromBody] int cpuCount) =>
        ExecuteVmActionAsync(id, VmAction.ResizeCpu, () => _virtualizationProvider.ResizeCpuAsync(id, cpuCount));

    [HttpPost("{id}/network-adapter")]
    public Task<IActionResult> AttachNetworkAdapter(string id, [FromBody] string switchName) =>
        ExecuteVmActionAsync(id, VmAction.AttachNetworkAdapter,
            () => _virtualizationProvider.AttachNetworkAdapterAsync(id, switchName));

    [HttpPost("{id}/vhd/resize")]
    public Task<IActionResult> ResizeVhd(string id, [FromBody] int sizeGb) =>
        ExecuteVmActionAsync(id, VmAction.VhdResize, () => _virtualizationProvider.ResizeVhdAsync(id, sizeGb));

    [HttpPost("{id}/vhd/compact")]
    public Task<IActionResult> CompactVhd(string id) =>
        ExecuteVmActionAsync(id, VmAction.VhdCompact, () => _virtualizationProvider.CompactVhdAsync(id));

    [HttpPost("{id}/export")]
    public Task<IActionResult> ExportVm(string id, [FromBody] string exportPath) =>
        ExecuteVmActionAsync(id, VmAction.Export, () => _virtualizationProvider.ExportVmAsync(id, exportPath));

    [HttpPost("{id}/import")]
    public Task<IActionResult> ImportVm(string id, [FromBody] string importPath) =>
        ExecuteVmActionAsync(id, VmAction.Import, () => _virtualizationProvider.ImportVmAsync(importPath));

    [HttpPost("{id}/clone")]
    public Task<IActionResult> CloneVm(string id, [FromBody] string newName) =>
        ExecuteVmActionAsync(id, VmAction.Clone, () => _virtualizationProvider.CloneVmAsync(id, newName));

    [HttpPost("{id}/migrate")]
    public Task<IActionResult> LiveMigrateVm(string id, [FromBody] string targetHost) =>
        ExecuteVmActionAsync(id, VmAction.LiveMigrate,
            () => _virtualizationProvider.LiveMigrateVmAsync(id, targetHost));

    private async Task<IActionResult> ExecuteVmActionAsync(string id, VmAction action, Func<Task> operation)
    {
        var (_, error) = await AuthorizeVmActionAsync(id, action);
        if (error is not null)
            return error;

        await operation();
        return Ok();
    }

    /// <summary>
    /// Autorisiert eine Aktion anhand der VM-Rolle aus dem "vmroles"-Claim des Tokens.
    /// Ohne Rolle für die betreffende VM gilt eine implizite Nicht-Berechtigung (403).
    /// </summary>
    private async Task<(VirtualMachine? Vm, IActionResult? Error)> AuthorizeVmActionAsync(string id, VmAction action)
    {
        var vm = await _virtualizationProvider.GetVmByIdAsync(id);
        if (vm is null)
            return (null, NotFound(new { message = "VM nicht gefunden" }));

        var vmRoles = GetVmRolesFromToken();
        if (!vmRoles.TryGetValue(vm.Name, out var role) || !RolePermissions.IsAllowed(role, action))
            return (null, Forbid());

        return (vm, null);
    }

    // FindAll statt FindFirst: Der JWT-Handler zerlegt den JSON-Array-Claim in
    // einen Claim pro VM-Rollen-Eintrag.
    private IReadOnlyDictionary<string, VmRole> GetVmRolesFromToken() =>
        VmRoleClaims.Deserialize(User.FindAll(VmRoleClaims.ClaimType).Select(claim => claim.Value));
}
