using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace UniVerein.Api.Helper;

public static class FileSignatureHelper
{
    private const int HeaderBufferSize = 16;
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47 };
    private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] GifSignature = { 0x47, 0x49, 0x46, 0x38 };
    private static readonly byte[] PdfSignature = { 0x25, 0x50, 0x44, 0x46 };
    private static readonly byte[] RiffSignature = { 0x52, 0x49, 0x46, 0x46 };
    private static readonly byte[] WebpSignature = { 0x57, 0x45, 0x42, 0x50 };

    public static async Task<string?> DetectContentTypeAsync(IFormFile file)
    {
        byte[] header = new byte[HeaderBufferSize];
        await using Stream stream = file.OpenReadStream();
        int read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        ReadOnlySpan<byte> bytes = header.AsSpan(0, read);

        if (bytes.StartsWith(PngSignature)) return "image/png";
        if (bytes.StartsWith(JpegSignature)) return "image/jpeg";
        if (bytes.StartsWith(GifSignature)) return "image/gif";
        if (bytes.StartsWith(PdfSignature)) return "application/pdf";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual(RiffSignature) && bytes[8..12].SequenceEqual(WebpSignature))
            return "image/webp";

        return null;
    }
}
