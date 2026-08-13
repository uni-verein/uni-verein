using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
    private readonly ReceiptService _receiptService;

    public BackupService(IConfiguration config, AppDbContext context, ReceiptService receiptService)
    {
        _config = config;
        _context = context;
        _receiptService = receiptService;
    }

    public virtual async Task WritePgDumpAsync(Stream output)
    {
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

        Task copyTask = process.StandardOutput.BaseStream.CopyToAsync(output);
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        await Task.WhenAll(copyTask, errorTask);
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception($"Backup failed: {await errorTask}");
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

    public virtual async Task WriteFullBackupZipAsync(Stream output)
    {
        string receiptsStoragePath = _receiptService.StoragePath;

        await using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry sqlEntry = archive.CreateEntry("database.sql");
            await using (Stream entryStream = sqlEntry.Open())
                await WritePgDumpAsync(entryStream);

            if (Directory.Exists(receiptsStoragePath))
            {
                foreach (string filePath in Directory.GetFiles(receiptsStoragePath, "*", SearchOption.AllDirectories))
                {
                    string entryName = "receipts/" +
                        Path.GetRelativePath(receiptsStoragePath, filePath).Replace('\\', '/');
                    await archive.CreateEntryFromFileAsync(filePath, entryName);
                }
            }
        }
    }

    public virtual async Task<bool> RestoreFullBackupAsync(IFormFile zipFile)
    {
        if (zipFile == null || zipFile.Length == 0)
            throw new ArgumentException("Invalid file");

        string tempZipPath = Path.Combine("/tmp", $"restore_full_{Guid.NewGuid()}.zip");
        await using (FileStream fileStream = File.Create(tempZipPath))
        {
            await zipFile.CopyToAsync(fileStream);
        }

        string extractDir = Path.Combine("/tmp", $"restore_full_{Guid.NewGuid()}");
        await ZipFile.ExtractToDirectoryAsync(tempZipPath, extractDir);

        try
        {
            string sqlPath = Path.Combine(extractDir, "database.sql");
            if (!File.Exists(sqlPath))
                throw new ArgumentException("Zip archive does not contain a database.sql file.");

            await using (FileStream sqlStream = File.OpenRead(sqlPath))
            {
                FormFile sqlFormFile = new(sqlStream, 0, sqlStream.Length, "file", "database.sql");
                await RestoreBackupAsync(sqlFormFile);
            }

            string receiptsExtractDir = Path.Combine(extractDir, "receipts");
            if (Directory.Exists(receiptsExtractDir))
            {
                string receiptsStoragePath = _receiptService.StoragePath;
                Directory.CreateDirectory(receiptsStoragePath);

                // Clear existing contents without removing the directory itself, since it may be a mount point.
                foreach (string existingFile in Directory.GetFiles(receiptsStoragePath, "*", SearchOption.AllDirectories))
                    File.Delete(existingFile);
                foreach (string existingDir in Directory.GetDirectories(receiptsStoragePath))
                    Directory.Delete(existingDir, recursive: true);

                foreach (string filePath in Directory.GetFiles(receiptsExtractDir, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(receiptsExtractDir, filePath);
                    string destination = Path.Combine(receiptsStoragePath, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(filePath, destination, overwrite: true);
                }
            }

            return true;
        }
        finally
        {
            File.Delete(tempZipPath);
            Directory.Delete(extractDir, recursive: true);
        }
    }
}