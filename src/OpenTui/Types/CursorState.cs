using System.Runtime.InteropServices;

namespace OpenTui;

/// <summary>Current state of the terminal cursor, as reported by the native renderer.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CursorState
{
    /// <summary>Cursor X position (column).</summary>
    public uint X;

    /// <summary>Cursor Y position (row).</summary>
    public uint Y;

    /// <summary>Whether the cursor is visible.</summary>
    [MarshalAs(UnmanagedType.U1)] public bool Visible;

    /// <summary>Raw cursor style byte.</summary>
    public byte Style;

    /// <summary>Whether the cursor is blinking.</summary>
    [MarshalAs(UnmanagedType.U1)] public bool Blinking;

    /// <summary>Red color component (0.0–1.0).</summary>
    public float R;

    /// <summary>Green color component (0.0–1.0).</summary>
    public float G;

    /// <summary>Blue color component (0.0–1.0).</summary>
    public float B;

    /// <summary>Alpha color component (0.0–1.0).</summary>
    public float A;

    /// <summary>Gets the cursor style.</summary>
    public readonly CursorStyle CursorStyle => (CursorStyle)Style;

    /// <summary>Gets the cursor color as an Rgba value.</summary>
    public readonly Rgba Color => new(R, G, B, A);
}
