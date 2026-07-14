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
        var vms = await _virtualizationProvider.GetVmsAsync();
        return Ok(vms);
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartVm(string id)
    {
        await _virtualizationProvider.StartVmAsync(id);
        return Ok();
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> StopVm(string id)
    {
        await _virtualizationProvider.StopVmAsync(id);
        return Ok();
    }

    [HttpPost("{id}/reset")]
    public async Task<IActionResult> ResetVm(string id)
    {
        await _virtualizationProvider.ResetVmAsync(id);
        return Ok();
    }

    [HttpPost("{id}/snapshot")]
    public async Task<IActionResult> CreateSnapshot(string id, [FromBody] string snapshotName)
    {
        await _virtualizationProvider.CreateSnapshotAsync(id, snapshotName);
        return Ok();
    }
}
