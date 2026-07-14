using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VmPortal.Core.Interfaces;

namespace VmPortal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VmController : ControllerBase
{
    private readonly IVirtualizationProvider _virtualizationProvider;

    public VmController(IVirtualizationProvider virtualizationProvider)
    {
        _virtualizationProvider = virtualizationProvider;
    }

    [HttpGet]
    public async Task<IActionResult> GetVms()
    {
        var username = GetCurrentUsername();
        var vms = await _virtualizationProvider.GetVmsAsync();
        return Ok(vms.Where(vm => vm.AssignedUserId == username));
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartVm(string id)
    {
        var accessError = await CheckVmAccessAsync(id);
        if (accessError is not null)
            return accessError;

        await _virtualizationProvider.StartVmAsync(id);
        return Ok();
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> StopVm(string id)
    {
        var accessError = await CheckVmAccessAsync(id);
        if (accessError is not null)
            return accessError;

        await _virtualizationProvider.StopVmAsync(id);
        return Ok();
    }

    [HttpPost("{id}/reset")]
    public async Task<IActionResult> ResetVm(string id)
    {
        var accessError = await CheckVmAccessAsync(id);
        if (accessError is not null)
            return accessError;

        await _virtualizationProvider.ResetVmAsync(id);
        return Ok();
    }

    [HttpPost("{id}/snapshot")]
    public async Task<IActionResult> CreateSnapshot(string id, [FromBody] string snapshotName)
    {
        var accessError = await CheckVmAccessAsync(id);
        if (accessError is not null)
            return accessError;

        await _virtualizationProvider.CreateSnapshotAsync(id, snapshotName);
        return Ok();
    }

    private string? GetCurrentUsername() => User.Identity?.Name;

    private async Task<IActionResult?> CheckVmAccessAsync(string id)
    {
        var vm = await _virtualizationProvider.GetVmByIdAsync(id);
        if (vm is null)
            return NotFound(new { message = "VM nicht gefunden" });

        if (vm.AssignedUserId != GetCurrentUsername())
            return Forbid();

        return null;
    }
}
