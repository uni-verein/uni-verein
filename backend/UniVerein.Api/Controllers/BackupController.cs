using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniVerein.Api.Extensions;
using UniVerein.Api.Models.Enums;
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
[DisabledInDemo]
public class BackupController : ControllerBase
{
    // Must match client_max_body_size for /api/backup/restore in nginx.conf.
    private const long MaxRestoreSizeBytes = 10L * 1024 * 1024 * 1024;

    private readonly BackupService _backup;
    private readonly AuditService _auditService;

    public BackupController(BackupService backup, AuditService auditService)
    {
        _backup = backup;
        _auditService = auditService;
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpGet]
    public async Task<IActionResult> GetBackup([FromQuery] bool full = false)
    {
        Log.Information("BackupController: GetBackup -> Start backup creation (full={Full}) by {ActorId}.", full, HttpContext.GetActorId());
        Stopwatch stopwatch = Stopwatch.StartNew();

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
                Log.Information("BackupController: GetBackup -> Full backup created in {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
                return new EmptyResult();
            }

            ContentDispositionHeaderValue sqlContentDisposition = new("attachment");
            sqlContentDisposition.SetHttpFileName($"backup_{DateTime.Now:yyyyMMdd}.sql");
            Response.ContentType = "application/sql";
            Response.Headers.ContentDisposition = sqlContentDisposition.ToString();

            await _backup.WritePgDumpAsync(Response.Body);
            Log.Information("BackupController: GetBackup -> SQL backup created in {ElapsedMs} ms.", stopwatch.ElapsedMilliseconds);
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            Log.Error("BackupController: GetBackup -> Backup failed after {ElapsedMs} ms, {ExceptionType}.",
                stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            throw;
        }
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost("restore")]
    [RequestSizeLimit(MaxRestoreSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRestoreSizeBytes)]
    public async Task<IActionResult> RestoreAsync(IFormFile file)
    {
        Log.Information("BackupController: RestoreAsync -> Try to restore a backup of {SizeBytes} bytes by {ActorId}.",
            file.Length, HttpContext.GetActorId());
        Stopwatch stopwatch = Stopwatch.StartNew();
        bool isZip;
        bool restored;
        try
        {
            isZip = file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                file.ContentType is "application/zip" or "application/x-zip-compressed";

            restored = isZip
                ? await _backup.RestoreFullBackupAsync(file)
                : await _backup.RestoreBackupAsync(file);

            if (restored)
                Log.Information("BackupController: RestoreAsync -> Restore ({Type}) completed in {ElapsedMs} ms.",
                    isZip ? "full" : "sql", stopwatch.ElapsedMilliseconds);
            else
                Log.Warning("BackupController: RestoreAsync -> Restore ({Type}) was not applied.", isZip ? "full" : "sql");
        }
        catch (Exception ex)
        {
            Log.Error("BackupController: RestoreAsync -> Restore failed after {ElapsedMs} ms, {ExceptionType}.",
                stopwatch.ElapsedMilliseconds, ex.GetType().Name);
            throw;
        }

        if (restored)
            await _auditService.LogAsync(AuditLogActions.RESTORE, "Backup",
                new { Type = isZip ? "full" : "sql", file.FileName, SizeBytes = file.Length });

        return Ok();
    }
}