using System.Text;

namespace OpenTui.Core.Native;

/// <summary>
/// Ref struct for efficient stack-allocated UTF-8 string marshalling.
/// Uses fixed/ReadOnlySpan for zero-alloc pinning during P/Invoke calls.
/// </summary>
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

    /// <summary>Pins the UTF-8 bytes and calls the function with (pointer, length), returning the result.</summary>
    public T WithPtr<T>(Func<nint, nuint, T> func)
    {
        if (Length == 0)
            return func(nint.Zero, 0);

        unsafe
        {
            fixed (byte* ptr = Bytes)
            {
                return func((nint)ptr, (nuint)Length);
            }
        }
    }

    /// <summary>Read a UTF-8 string from a native output buffer using a getter that writes to a buffer.</summary>
    public static string GetString(Func<nint, nuint, nuint> getter, int maxLen = 4096)
    {
        var buf = new byte[maxLen];
        nuint actualLen;
        unsafe
        {
            fixed (byte* ptr = buf)
            {
                actualLen = getter((nint)ptr, (nuint)maxLen);
            }
        }
        return Encoding.UTF8.GetString(buf, 0, (int)actualLen);
    }
}
