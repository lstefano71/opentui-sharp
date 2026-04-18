using System.Text;
using OpenTui.Core.Native;
using OpenTui.Native;

namespace OpenTui.Core;

/// <summary>
/// Static helpers for the native hyperlink (OSC 8) system.
/// Links are renderer-managed — no separate destroy needed.
/// </summary>
public static class Link
{
    /// <summary>Allocates a new link ID for the given URL.</summary>
    public static uint Alloc(string url)
    {
        byte[] urlBytes = Encoding.UTF8.GetBytes(url);
        unsafe
        {
            fixed (byte* ptr = urlBytes)
            {
                return OpenTuiNative.LinkAlloc((nint)ptr, (uint)urlBytes.Length);
            }
        }
    }

    /// <summary>Gets the URL associated with a link ID.</summary>
    public static string GetUrl(uint linkId)
    {
        return Utf8String.GetString((outPtr, maxLen) =>
            (nuint)OpenTuiNative.LinkGetUrl(linkId, outPtr, (uint)maxLen));
    }

    /// <summary>Combines cell attributes with a link ID.</summary>
    public static uint AttributesWithLink(uint attrs, uint linkId) =>
        OpenTuiNative.AttributesWithLink(attrs, linkId);

    /// <summary>Extracts the link ID from combined cell attributes.</summary>
    public static uint GetLinkId(uint attrs) =>
        OpenTuiNative.AttributesGetLinkId(attrs);
}
