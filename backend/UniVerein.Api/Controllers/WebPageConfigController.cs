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

[ApiController]
[Route("web-page-config")]
public class WebPageConfigController : ControllerBase
{
    private readonly AppDbContext _db;

    public WebPageConfigController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<WebPageConfigResult>> GetAsync()
    {
        WebPageConfigEntity? webPageConfig = await _db.WebPageConfigs.FirstOrDefaultAsync(x => x.DeletedAt == null);
        if (webPageConfig == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Web page config not found.",
                moreInfo: "Web page config not found. Web page config not configured."));

        WebPageConfigResult result = new()
        {
            Id = webPageConfig.Id,
            PageName = webPageConfig.PageName,
            Logo = webPageConfig.Logo
        };

        return Ok(result);
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPut]
    public async Task<ActionResult<WebPageConfigResult>> UpdateAsync([FromBody] WebPageConfigRequest request)
    {
        WebPageConfigEntity? webPageConfig = await _db.WebPageConfigs.FirstOrDefaultAsync();
        if (webPageConfig == null)
        {
            webPageConfig = new()
            {
                PageName = request.PageName,
                Logo = request.Logo
            };
            await _db.WebPageConfigs.AddAsync(webPageConfig);
        }
        else
        {
            webPageConfig.PageName = request.PageName;
            webPageConfig.Logo = request.Logo;
            webPageConfig.DeletedAt = null;
            _db.WebPageConfigs.Update(webPageConfig);
        }

        await _db.SaveChangesAsync();
        return Ok(new WebPageConfigResult()
        {
            Id = webPageConfig.Id,
            PageName = webPageConfig.PageName,
            Logo = webPageConfig.Logo
        });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        WebPageConfigEntity? webPageConfig = await _db.WebPageConfigs.FindAsync(id);
        if (webPageConfig == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Web page config not found.",
                moreInfo: $"Web page config with ID {id} not found."));

        _db.Remove(webPageConfig);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [Authorize]
    [HttpGet("sidebar")]
    public async Task<ActionResult<SidebarResult>> GetSidebarSettingsAsync()
    {
        bool creditorConfig = await _db.CreditorConfigs.AnyAsync(x => x.DeletedAt == null);
        bool mailConfig = await _db.MailSettings.AnyAsync(x => x.DeletedAt == null);
        List<LinkResult> linkSettings = await _db.LinkSettings.Where(x => x.DeletedAt == null).Select(y =>
            new LinkResult()
            {
                Link = y.Link,
                Name = y.Name,
                Icon = y.Icon
            }).ToListAsync();

        return Ok(new SidebarResult()
        {
            ShowSepa = creditorConfig,
            ShowMail = mailConfig,
            Links = linkSettings
        });
    }
}