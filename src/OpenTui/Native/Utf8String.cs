using System.Text;

namespace OpenTui;

/// <summary>Ref struct for efficient stack-allocated UTF-8 string marshalling.</summary>
internal ref struct Utf8String
{
    private readonly byte[]? _heapBuffer;

    /// <summary>The UTF-8 encoded bytes.</summary>
    public readonly ReadOnlySpan<byte> Bytes;

    /// <summary>The byte length of the encoded string.</summary>
    public readonly int Length;

    public Utf8String(string? value)
    {
        if (value is null or { Length: 0 })
        {
            Bytes = ReadOnlySpan<byte>.Empty;
            Length = 0;
            return;
        }

        int maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);
        _heapBuffer = new byte[maxBytes];
        Length = Encoding.UTF8.GetBytes(value, _heapBuffer);
        Bytes = _heapBuffer.AsSpan(0, Length);
    }

    /// <summary>Pins the UTF-8 bytes and calls the action with (pointer, length).</summary>
    public void WithPtr(Action<nint, nuint> action)
    {
        if (Length == 0)
        {
            action(nint.Zero, 0);
            return;
        }

        unsafe
        {
            fixed (byte* ptr = Bytes)
            {
                action((nint)ptr, (nuint)Length);
            }
        }
    }
}
