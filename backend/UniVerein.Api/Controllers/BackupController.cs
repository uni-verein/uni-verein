using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using Serilog;

namespace UniVerein.Api.Controllers;

[ApiController]
[Route("backup")]
[EnableCors("AllowFrontend")]
public class BackupController : ControllerBase
{
    private readonly BackupService _backup;

    public BackupController(BackupService backup)
    {
        _backup = backup;
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpGet]
    public async Task<IActionResult> GetBackup([FromQuery] bool full = false)
    {
        Log.Information($"BackupController: Start backup-creation (full={full})");

        try
        {
            if (full)
            {
                IHttpBodyControlFeature? bodyControlFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
                if (bodyControlFeature != null)
                    bodyControlFeature.AllowSynchronousIO = true;

                ContentDispositionHeaderValue contentDisposition = new("attachment");
                contentDisposition.SetHttpFileName($"backup_full_{DateTime.Now:yyyyMMdd}.zip");
                Response.ContentType = "application/zip";
                Response.Headers.ContentDisposition = contentDisposition.ToString();

                await _backup.WriteFullBackupZipAsync(Response.Body);
                Log.Information($"BackupController: Full backup backup_full_{DateTime.Now:yyyyMMdd}.zip created");
                return new EmptyResult();
            }

            ContentDispositionHeaderValue sqlContentDisposition = new("attachment");
            sqlContentDisposition.SetHttpFileName($"backup_{DateTime.Now:yyyyMMdd}.sql");
            Response.ContentType = "application/sql";
            Response.Headers.ContentDisposition = sqlContentDisposition.ToString();

            await _backup.WritePgDumpAsync(Response.Body);
            Log.Information($"BackupController: Backup backup_{DateTime.Now:yyyyMMdd}.sql created");
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            Log.Error($"BackupController: Error on backup: {ex.Message}", ex);
            throw;
        }
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost("restore")]
    public async Task<IActionResult> RestoreAsync(IFormFile file)
    {
        Log.Information($"BackupController: Try to restore {file.FileName}");
        try
        {
            bool isZip = file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                file.ContentType is "application/zip" or "application/x-zip-compressed";

            bool restored = isZip
                ? await _backup.RestoreFullBackupAsync(file)
                : await _backup.RestoreBackupAsync(file);

            if (restored)
                Log.Information("BackupController: Restoring of backup completed");
        }
        catch (Exception ex)
        {
            Log.Error($"BackupController: Error on restore backup: {ex.Message}", ex);
            throw;
        }

        return Ok();
    }
}