namespace UniVerein.IntegrationTests.Infrastructure;

// Returns at most chunkSize bytes per read, to hit every possible chunk boundary.
internal sealed class ChunkedStream(byte[] data, int chunkSize) : Stream
{
    private int _position;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => data.Length;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int n = Math.Min(Math.Min(count, chunkSize), data.Length - _position);
        Buffer.BlockCopy(data, _position, buffer, offset, n);
        _position += n;
        return n;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int n = Math.Min(Math.Min(buffer.Length, chunkSize), data.Length - _position);
        data.AsMemory(_position, n).CopyTo(buffer);
        _position += n;
        return ValueTask.FromResult(n);
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
