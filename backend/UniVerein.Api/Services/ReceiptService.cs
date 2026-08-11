using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serilog;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Services;

public class ReceiptService
{
    private readonly string _storagePath;

    public ReceiptService(IConfiguration config)
    {
        _storagePath = config["RECEIPT_STORAGE_PATH"] ?? "/app/receipts";
    }

    public string StoragePath
    {
        get { return _storagePath; }
    }

    public async Task<ReceiptFileEntity> SaveFileAsync(ReceiptEntity receipt, IFormFile file, int position)
    {
        Directory.CreateDirectory(_storagePath);

        ReceiptFileEntity receiptFile = new()
        {
            Id = Guid.NewGuid(),
            ReceiptId = receipt.Id,
            Receipt = receipt,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            Position = position
        };

        string path = GetFilePath(receiptFile);
        await using FileStream stream = File.Create(path);
        await file.CopyToAsync(stream);

        return receiptFile;
    }

    public string GetFilePath(ReceiptFileEntity receiptFile)
    {
        return Path.Combine(_storagePath, receiptFile.Id.ToString());
    }

    public void DeleteFiles(IEnumerable<ReceiptFileEntity> files)
    {
        foreach (ReceiptFileEntity receiptFile in files)
        {
            string path = GetFilePath(receiptFile);
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    public virtual async Task WriteExportZipAsync(Stream output, byte[] csvBytes, List<ReceiptFileEntity> files)
    {
        await using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry csvEntry = archive.CreateEntry("receipts.csv");
            await using (Stream entryStream = csvEntry.Open())
                await entryStream.WriteAsync(csvBytes);

            foreach (ReceiptFileEntity file in files)
            {
                string path = GetFilePath(file);
                if (!File.Exists(path))
                {
                    Log.Warning($"ReceiptService: WriteExportZipAsync -> File {file.Id} missing on disk, skipped.");
                    continue;
                }

                string entryName = $"files/{file.Id}.{GetExtensionFromContentType(file.ContentType)}";
                await archive.CreateEntryFromFileAsync(path, entryName);
            }
        }
    }

    private static string GetExtensionFromContentType(string contentType)
    {
        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return "pdf";

        const string imagePrefix = "image/";
        if (contentType.StartsWith(imagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            string subtype = contentType[imagePrefix.Length..];
            int plusIndex = subtype.IndexOf('+');
            return plusIndex >= 0 ? subtype[..plusIndex] : subtype;
        }

        return "bin";
    }
}
