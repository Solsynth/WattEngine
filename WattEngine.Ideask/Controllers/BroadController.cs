using System.ComponentModel.DataAnnotations;
using DysonNetwork.Shared.Proto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WattEngine.Ideask.Services;

namespace WattEngine.Ideask.Controllers;

[ApiController]
[Route("/api/broads")]
public class BroadController(BroadService broadService) : ControllerBase
{
    public record CreateBroadRequest(
        [Required]
        [MinLength(1)]
        [MaxLength(256)]
        string Name,
        Guid? ProjectId
    );

    public record UpdateBroadRequest(
        [Required]
        [MinLength(1)]
        [MaxLength(256)]
        string Name,
        Guid? ProjectId
    );

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ListBroads()
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        var broads = await broadService.GetBroadsAsync();
        return Ok(broads);
    }

    [HttpGet("{broadId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetBroad([FromRoute] Guid broadId)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        var broad = await broadService.GetBroadAsync(broadId);
        if (broad == null) return NotFound("Broad not found or no access");
        return Ok(broad);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateBroad([FromBody] CreateBroadRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            var broad = await broadService.CreateBroadAsync(request.Name, request.ProjectId);
            return CreatedAtAction(
                nameof(GetBroad),
                new { broadId = broad.Id },
                broad
            );
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{broadId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateBroad([FromRoute] Guid broadId, [FromBody] UpdateBroadRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            var broad = await broadService.UpdateBroadAsync(broadId, request.Name, request.ProjectId);
            return Ok(broad);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Broad not found");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{broadId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteBroad([FromRoute] Guid broadId)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            await broadService.DeleteBroadAsync(broadId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Broad not found");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}