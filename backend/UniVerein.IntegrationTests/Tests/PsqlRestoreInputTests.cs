using System.Text;
using Shouldly;
using UniVerein.Api.Helper;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class PsqlRestoreInputTests
{
    private const string Key = "OURKEY0123456789";

    [Fact]
    public async Task Output_StartsWithRestrictCommandOfOurKey()
    {
        string output = await RunAsync("select 1;\n");

        output.ShouldBe($"\\restrict {Key}\nselect 1;\n");
    }

    [Fact]
    public async Task EmptyInput_OnlyWritesTheRestrictCommand()
    {
        string output = await RunAsync("");

        output.ShouldBe($"\\restrict {Key}\n");
    }

    [Fact]
    public async Task RestrictLinesOfPgDump_AreRemoved()
    {
        string dump = "--\n\\restrict 0JX6zvsfOZcZRyNJ8eXK1Vo\nselect 1;\n\\unrestrict 0JX6zvsfOZcZRyNJ8eXK1Vo\n";

        string output = await RunAsync(dump);

        output.ShouldBe($"\\restrict {Key}\n--\nselect 1;\n");
    }

    [Fact]
    public async Task RestrictLinesWithCrLf_AreRemoved()
    {
        string output = await RunAsync("\\restrict abc\r\nselect 1;\r\n\\unrestrict abc\r\n");

        output.ShouldBe($"\\restrict {Key}\nselect 1;\r\n");
    }

    [Fact]
    public async Task ForgedUnrestrictWithOurKey_IsRemoved()
    {
        // The key is random per restore and unknown to the uploader, but even a correct guess must not reach psql.
        string output = await RunAsync($"select 1;\n\\unrestrict {Key}\n\\! id\n");

        output.ShouldNotContain($"\\unrestrict {Key}");
        output.ShouldBe($"\\restrict {Key}\nselect 1;\n\\! id\n");
    }

    [Theory]
    [InlineData("\\restrict\n")]
    [InlineData("\\restrict \n")]
    [InlineData("\\restrict a-b\n")]
    [InlineData("\\restrict a b\n")]
    [InlineData("\\restrictabc\n")]
    [InlineData(" \\restrict abc\n")]
    [InlineData("\\unrestrict\n")]
    [InlineData("\\unrestrict a;b\n")]
    public async Task LookalikeLines_AreKept(string line)
    {
        string output = await RunAsync(line);

        output.ShouldBe($"\\restrict {Key}\n" + line);
    }

    [Fact]
    public async Task OtherBackslashLines_AreKept_AndLeftToPsqlRestrictedMode()
    {
        string input = "COPY t (id, note) FROM stdin;\n1\ta\\b\n2\t\\N\n\\.\n\\! id\n\\connect other\n";

        string output = await RunAsync(input);

        output.ShouldBe($"\\restrict {Key}\n" + input);
    }

    [Fact]
    public async Task LastLineWithoutNewline_IsHandled()
    {
        (await RunAsync("select 1;\n\\unrestrict abc")).ShouldBe($"\\restrict {Key}\nselect 1;\n");
        (await RunAsync("select 1;\n\\! id")).ShouldBe($"\\restrict {Key}\nselect 1;\n\\! id");
    }

    [Fact]
    public async Task VeryLongBackslashLine_PassesThroughUnchanged()
    {
        string longLine = "\\" + new string('x', 20_000) + "\n";

        string output = await RunAsync(longLine + "select 1;\n");

        output.ShouldBe($"\\restrict {Key}\n" + longLine + "select 1;\n");
    }

    [Fact]
    public async Task OtherBytes_AreCopiedUnchanged()
    {
        // Arbitrary bytes, raw carriage returns and a long line without any newline must survive untouched.
        Random random = new(42);
        byte[] payload = new byte[300_000];
        random.NextBytes(payload);
        for (int i = 0; i < payload.Length; i++)
            if (payload[i] == (byte)'\\' || payload[i] == (byte)'\n')
                payload[i] = (byte)'a';
        payload[10] = (byte)'\r';
        payload[11] = (byte)'\n';

        byte[] output = await RunBytesAsync(payload, chunkSize: 0);

        output.AsSpan(("\\restrict " + Key + "\n").Length).ToArray().ShouldBe(payload);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(64)]
    public async Task ResultDoesNotDependOnHowTheInputIsChunked(int chunkSize)
    {
        string input = "--\n\\restrict abc123\nselect 'a\\b';\nCOPY t FROM stdin;\n1\t\\N\n\\.\n" +
                       "\\unrestrict abc123\n\\! id\n\\unrestrict last";

        byte[] whole = await RunBytesAsync(Encoding.UTF8.GetBytes(input), chunkSize: 0);
        byte[] chunked = await RunBytesAsync(Encoding.UTF8.GetBytes(input), chunkSize);

        chunked.ShouldBe(whole);
        Encoding.UTF8.GetString(chunked).ShouldNotContain("restrict abc123");
    }

    [Fact]
    public void CreateRestrictKey_IsLongRandomAndAlphanumeric()
    {
        string first = PsqlRestoreInput.CreateRestrictKey();
        string second = PsqlRestoreInput.CreateRestrictKey();

        first.Length.ShouldBeGreaterThanOrEqualTo(32);
        first.ShouldNotBe(second);
        first.ShouldAllBe(c => char.IsAsciiLetterOrDigit(c));
        PsqlRestoreInput.IsRestrictLine(Encoding.ASCII.GetBytes($"\\restrict {first}\n")).ShouldBeTrue();
    }

    private static async Task<string> RunAsync(string input)
    {
        return Encoding.UTF8.GetString(await RunBytesAsync(Encoding.UTF8.GetBytes(input), chunkSize: 0));
    }

    private static async Task<byte[]> RunBytesAsync(byte[] input, int chunkSize)
    {
        await using Stream source = chunkSize > 0
            ? new ChunkedStream(input, chunkSize)
            : new MemoryStream(input);
        using MemoryStream destination = new();

        await PsqlRestoreInput.CopyRestrictedAsync(source, destination, Key);

        return destination.ToArray();
    }
}
