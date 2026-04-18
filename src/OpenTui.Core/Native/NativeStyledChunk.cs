using System.Runtime.InteropServices;

namespace OpenTui.Core.Native;

/// <summary>
/// Matches the Zig <c>StyledChunk</c> extern struct layout for FFI.
/// Used by <c>textBufferSetStyledText</c> to pass styled text chunks to native code.
/// The native side copies all text data into its own buffer, so pinned pointers
/// only need to survive the duration of the P/Invoke call.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeStyledChunk
{
    public nint TextPtr;      // [*]const u8
    public nuint TextLen;     // usize
    public nint FgPtr;        // ?[*]const f32 (4 floats: r,g,b,a) or null
    public nint BgPtr;        // ?[*]const f32 (4 floats: r,g,b,a) or null
    public uint Attributes;   // u32
    // Sequential layout inserts 4-byte padding here on 64-bit to align LinkPtr
    public nint LinkPtr;      // ?[*]const u8 or null
    public nuint LinkLen;     // usize
}
