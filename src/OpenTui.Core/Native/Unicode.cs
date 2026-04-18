using System.Text;
using OpenTui.Native;

namespace OpenTui.Core;

/// <summary>
/// Wrapper for the native Unicode encoding functions.
/// </summary>
public static class Unicode
{
    /// <summary>
    /// Encodes text using the native Unicode width calculation.
    /// Returns the encoded bytes, or null if encoding failed.
    /// The caller must call <see cref="FreeEncoded"/> on the result when done.
    /// </summary>
    public static unsafe (nint Data, nuint Length)? Encode(string text, WidthMethod method = WidthMethod.Wcwidth)
    {
        byte[] input = Encoding.UTF8.GetBytes(text);
        nint outBuf;
        nuint outLen;

        fixed (byte* inputPtr = input)
        {
            bool ok = OpenTuiNative.EncodeUnicode(
                (nint)inputPtr, (nuint)input.Length,
                (nint)(&outBuf), (nint)(&outLen),
                (byte)method);

            if (!ok)
                return null;
        }

        return (outBuf, outLen);
    }

    /// <summary>Frees a buffer previously allocated by <see cref="Encode"/>.</summary>
    public static void FreeEncoded(nint data, nuint len) =>
        OpenTuiNative.FreeUnicode(data, len);
}
