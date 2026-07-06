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
            FileName = "mariadb-dump",
            Arguments =
                $"-h db -u {_config["ConnectionStrings:MysqlUser"]} --single-transaction --quick {_config["ConnectionStrings:Database"]}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["MYSQL_PWD"] = _config["ConnectionStrings:MysqlPassword"];

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
            FileName = "mariadb",
            Arguments = $"-h db -u {_config["ConnectionStrings:MysqlUser"]} {_config["ConnectionStrings:Database"]}",
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["MYSQL_PWD"] = _config["ConnectionStrings:MysqlPassword"];

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