using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UniVerein.Api.Helper;

// A restore pipes an uploaded SQL file into psql. psql executes backslash commands from its input, e.g. "\! cmd" runs a
// shell command as the backend user, so an uploaded file must not be able to use them. Since PostgreSQL 17.6 psql has a
// restricted mode: after "\restrict KEY" every backslash command except "\unrestrict KEY" is refused, and the key is
// only known to us. pg_dump (17.6 and later) wraps its own output in such a pair with a key it picked, which would
// collide with ours, so those two kinds of lines are removed before the file reaches psql. Removing them cannot
// execute anything and leaves the restricted mode of the key we chose in place until the end of the input.
public static class PsqlRestoreInput
{
    private const int MaxBackslashLineBytes = 4096;
    private const int BufferSize = 81920;

    public static string CreateRestrictKey()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    public static async Task CopyRestrictedAsync(Stream source, Stream destination, string restrictKey,
        CancellationToken cancellationToken = default)
    {
        await destination.WriteAsync(Encoding.ASCII.GetBytes($"\\restrict {restrictKey}\n"), cancellationToken);

        byte[] buffer = new byte[BufferSize];
        byte[] pending = new byte[MaxBackslashLineBytes];
        int pendingLength = 0;
        bool collecting = false;
        bool atLineStart = true;

        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            int index = 0;
            while (index < read)
            {
                int newline = Array.IndexOf(buffer, (byte)'\n', index, read - index);
                int end = newline < 0 ? read : newline + 1;

                if (collecting)
                {
                    int take = end - index;
                    if (pendingLength + take > MaxBackslashLineBytes)
                    {
                        await destination.WriteAsync(pending.AsMemory(0, pendingLength), cancellationToken);
                        await destination.WriteAsync(buffer.AsMemory(index, take), cancellationToken);
                        pendingLength = 0;
                        collecting = false;
                        atLineStart = newline >= 0;
                    }
                    else
                    {
                        Buffer.BlockCopy(buffer, index, pending, pendingLength, take);
                        pendingLength += take;
                        if (newline >= 0)
                        {
                            if (!IsRestrictLine(pending.AsSpan(0, pendingLength)))
                                await destination.WriteAsync(pending.AsMemory(0, pendingLength), cancellationToken);
                            pendingLength = 0;
                            collecting = false;
                            atLineStart = true;
                        }
                    }

                    index = end;
                }
                else if (atLineStart && buffer[index] == (byte)'\\')
                {
                    collecting = true;
                    pendingLength = 0;
                }
                else
                {
                    await destination.WriteAsync(buffer.AsMemory(index, end - index), cancellationToken);
                    atLineStart = newline >= 0;
                    index = end;
                }
            }
        }

        if (collecting && pendingLength > 0 && !IsRestrictLine(pending.AsSpan(0, pendingLength)))
            await destination.WriteAsync(pending.AsMemory(0, pendingLength), cancellationToken);
    }

    // Exactly "\restrict KEY" or "\unrestrict KEY" with an alphanumeric key, as pg_dump writes them.
    public static bool IsRestrictLine(ReadOnlySpan<byte> line)
    {
        if (line.EndsWith("\n"u8))
            line = line[..^1];
        if (line.EndsWith("\r"u8))
            line = line[..^1];

        ReadOnlySpan<byte> key;
        if (line.StartsWith("\\restrict "u8))
            key = line["\\restrict "u8.Length..];
        else if (line.StartsWith("\\unrestrict "u8))
            key = line["\\unrestrict "u8.Length..];
        else
            return false;

        if (key.IsEmpty)
            return false;

        foreach (byte b in key)
        {
            bool alphanumeric = b is >= (byte)'0' and <= (byte)'9' or >= (byte)'a' and <= (byte)'z' or
                >= (byte)'A' and <= (byte)'Z';
            if (!alphanumeric)
                return false;
        }

        return true;
    }
}
