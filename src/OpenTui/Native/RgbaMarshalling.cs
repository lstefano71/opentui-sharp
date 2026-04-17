namespace OpenTui;

/// <summary>Helpers for marshalling Rgba colors to the native 4×float RGBA format.</summary>
internal static class RgbaMarshalling
{
    /// <summary>Pins an Rgba color as a float[4] span and calls the action with the pointer.</summary>
    internal static void WithColorPtr(Rgba color, Action<nint> action)
    {
        Span<float> rgba = [color.R, color.G, color.B, color.A];
        unsafe
        {
            fixed (float* ptr = rgba)
            {
                action((nint)ptr);
            }
        }
    }

    /// <summary>Pins two Rgba colors and calls the action with both pointers.</summary>
    internal static void WithColorPtrs(Rgba color1, Rgba color2, Action<nint, nint> action)
    {
        Span<float> rgba1 = [color1.R, color1.G, color1.B, color1.A];
        Span<float> rgba2 = [color2.R, color2.G, color2.B, color2.A];
        unsafe
        {
            fixed (float* ptr1 = rgba1)
            fixed (float* ptr2 = rgba2)
            {
                action((nint)ptr1, (nint)ptr2);
            }
        }
    }
}
