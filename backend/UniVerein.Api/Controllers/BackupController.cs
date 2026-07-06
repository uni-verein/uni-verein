using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
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
    public async Task<IActionResult> GetBackup()
    {
        try
        {
            Log.Information("BackupController: Start backup-creation");
            var path = await _backup.CreateBackupAsync();

            if (!System.IO.File.Exists(path))
            {
                Log.Error("BackupController: Backup-file couldn't be found.");
                return NotFound("Backup-file couldn't be found.");
            }

            FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.DeleteOnClose);

            Log.Information($"BackupController: Backup backup_{DateTime.Now:yyyyMMdd}.sql created");
            return File(stream, "application/sql", $"backup_{DateTime.Now:yyyyMMdd}.sql");
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
            if (await _backup.RestoreBackupAsync(file))
                Log.Information($"BackupController: Restoring of backup completed");
        }
        catch (Exception ex)
        {
            Log.Error($"BackupController: Error on restore backup: {ex.Message}", ex);
            throw;
        }

        return Ok();
    }
}