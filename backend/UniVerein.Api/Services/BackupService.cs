using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UniVerein.DAL.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace UniVerein.Api.Services;

public class BackupService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _context;

    public BackupService(IConfiguration config, AppDbContext context)
    {
        _config = config;
        _context = context;
    }

    public virtual async Task<string> CreateBackupAsync()
    {
        string filePath = Path.Combine("/tmp", $"backup_{DateTime.Now:yyyyMMddHHmm}.sql");

        ProcessStartInfo psi = new()
        {
            FileName = "pg_dump",
            Arguments =
                $"-h db -U {_config["ConnectionStrings:DbUser"]} --format=plain --no-owner --clean --if-exists {_config["ConnectionStrings:Database"]}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["PGPASSWORD"] = _config["ConnectionStrings:DbPassword"];

        using Process process = new() { StartInfo = psi };
        process.Start();

        await using FileStream fileStream = File.Create(filePath);

        Task copyTask = process.StandardOutput.BaseStream.CopyToAsync(fileStream);
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(copyTask, errorTask);
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception($"Backup failed: {await errorTask}");

        return filePath;
    }

    public virtual async Task<bool> RestoreBackupAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Invalid file");

        string path = "/tmp/restore.sql";
        await using (FileStream fileStream = File.Create(path))
        {
            await file.CopyToAsync(fileStream);
        }

        ProcessStartInfo psi = new()
        {
            FileName = "psql",
            Arguments =
                $"-h db -U {_config["ConnectionStrings:DbUser"]} -d {_config["ConnectionStrings:Database"]} -v ON_ERROR_STOP=1",
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["PGPASSWORD"] = _config["ConnectionStrings:DbPassword"];

        using Process process = new() { StartInfo = psi };
        process.Start();

        await using (FileStream sqlStream = File.OpenRead(path))
        {
            await sqlStream.CopyToAsync(process.StandardInput.BaseStream);
        }

        process.StandardInput.Close();

        string errors = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception($"Restore failed: {errors}");

        await _context.Database.MigrateAsync();

        return true;
    }
}