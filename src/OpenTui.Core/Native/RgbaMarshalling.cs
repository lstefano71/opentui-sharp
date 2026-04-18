namespace OpenTui.Core.Native;

/// <summary>
/// Helpers for marshalling Rgba colors to the native 4×float RGBA format.
/// Uses stack-allocated Span + fixed for zero-alloc pinning.
/// </summary>
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

    /// <summary>Pins an Rgba color (or passes nint.Zero for null) and calls the action.</summary>
    internal static void WithColorPtr(Rgba? color, Action<nint> action)
    {
        if (color is null)
        {
            action(nint.Zero);
            return;
        }
        WithColorPtr(color.Value, action);
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

    /// <summary>Pins two nullable Rgba colors and calls the action with both pointers.</summary>
    internal static void WithColorPtrs(Rgba? color1, Rgba? color2, Action<nint, nint> action)
    {
        WithColorPtr(color1, ptr1 => WithColorPtr(color2, ptr2 => action(ptr1, ptr2)));
    }

    /// <summary>Pins a flat float array of multiple colors and calls the action with the pointer.</summary>
    internal static void WithColorArrayPtr(Rgba[] colors, Action<nint> action)
    {
        var floats = new float[colors.Length * 4];
        for (int i = 0; i < colors.Length; i++)
        {
            floats[i * 4] = colors[i].R;
            floats[i * 4 + 1] = colors[i].G;
            floats[i * 4 + 2] = colors[i].B;
            floats[i * 4 + 3] = colors[i].A;
        }

        unsafe
        {
            fixed (float* ptr = floats)
            {
                action((nint)ptr);
            }
        }
    }
}
