using System;
using System.IO;

namespace Instrumentarria.CustomSoundBankReader;

public class SubStreamFromFile : Stream
{
    private FileStream _file;
    private readonly long _start;
    private readonly long _length;
    private long _position;

    public SubStreamFromFile(string filePath, long offset, long length)
    {
        _file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _start = offset;
        _length = length;
        _position = 0;

        _file.Seek(_start, SeekOrigin.Begin);
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;

    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= _length)
            return 0;

        long remaining = _length - _position;
        if (count > remaining)
            count = (int)remaining;

        int read = _file.Read(buffer, offset, count);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException()
        };

        if (target < 0 || target > _length)
            throw new IOException("Seek out of bounds");

        _position = target;
        _file.Seek(_start + _position, SeekOrigin.Begin);
        return _position;
    }

    protected override void Dispose(bool disposing)
    {
        _file?.Dispose();
        _file = null;
        base.Dispose(disposing);
    }

    public override void Flush() => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
