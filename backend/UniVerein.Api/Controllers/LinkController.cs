using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("link")]
public class LinkController : ControllerBase
{
    private readonly AppDbContext _db;

    public LinkController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<AllLinkSettingsResults>> GetAllAsync()
    {
        List<LinkSettingsEntity> linkSettings = await _db.LinkSettings.Where(x => x.DeletedAt == null).ToListAsync();

        AllLinkSettingsResults result = new()
        {
            Items = linkSettings.Select(x => new LinkSettingsResult()
            {
                Id = x.Id,
                Link = x.Link,
                Name = x.Name,
                Icon = x.Icon
            }).ToList(),
            Total = linkSettings.Count
        };

        return Ok(result);
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost]
    public async Task<ActionResult<LinkSettingsResult>> CreateAsync([FromBody] LinkSettingsRequest request)
    {
        if (request.Link.Length > 100)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "The link contains too many characters. The maximum length is 100."));

        if (request.Name.Length > 20)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "The name contains too many characters. The maximum length is 20."));

        LinkSettingsEntity linkSetting = new()
        {
            Id = Guid.NewGuid(),
            Link = request.Link,
            Name = request.Name,
            Icon = request.Icon
        };

        await _db.LinkSettings.AddAsync(linkSetting);
        await _db.SaveChangesAsync();

        return Ok(new LinkSettingsResult()
        {
            Id = linkSetting.Id,
            Link = linkSetting.Link,
            Icon = linkSetting.Icon,
            Name = linkSetting.Name
        });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPatch("{id}")]
    public async Task<ActionResult<LinkSettingsResult>> UpdateAsync([FromRoute] Guid id,
        [FromBody] LinkSettingsUpdateRequest request)
    {
        LinkSettingsEntity? linkSetting =
            await _db.LinkSettings.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (linkSetting == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Link not found.",
                moreInfo: $"Link with ID {id} not found."));

        if (!string.IsNullOrWhiteSpace(request.Link))
        {
            if (request.Link.Length > 100)
                return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                    moreInfo: "The link contains too many characters. The maximum length is 100."));
            linkSetting.Link = request.Link;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            if (request.Name.Length > 20)
                return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                    moreInfo: "The name contains too many characters. The maximum length is 20."));
            linkSetting.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.Icon))
            linkSetting.Icon = request.Icon;

        _db.LinkSettings.Update(linkSetting);
        await _db.SaveChangesAsync();

        return Ok(new LinkSettingsResult()
        {
            Id = linkSetting.Id,
            Link = linkSetting.Link,
            Name = linkSetting.Name,
            Icon = linkSetting.Icon
        });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        LinkSettingsEntity? linkSetting = await _db.LinkSettings.FindAsync(id);
        if (linkSetting == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Link not found.",
                moreInfo: $"Link with ID {id} not found."));

        _db.Remove(linkSetting);
        await _db.SaveChangesAsync();
        return Ok();
    }
}