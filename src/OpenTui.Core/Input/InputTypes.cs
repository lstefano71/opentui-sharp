namespace OpenTui.Core;

/// <summary>Type of key event in the Kitty keyboard protocol.</summary>
public enum KeyEventType : byte
{
    Press = 0,
    Repeat = 1,
    Release = 2,
}

/// <summary>Type of mouse interaction.</summary>
public enum MouseEventType : byte
{
    Down,
    Up,
    Move,
    Drag,
    DragEnd,
    Drop,
    Over,
    Out,
    Scroll,
}

/// <summary>Mouse button constants.</summary>
public enum MouseButton : byte
{
    Left = 0,
    Middle = 1,
    Right = 2,
    WheelUp = 4,
    WheelDown = 5,
    WheelLeft = 6,
    WheelRight = 7,
}

/// <summary>Mouse encoding format on the wire.</summary>
public enum MouseEncoding : byte
{
    Sgr,
    X10,
}

/// <summary>Scroll direction and delta.</summary>
public readonly record struct ScrollInfo(string Direction, int Delta);

/// <summary>Modifier key state.</summary>
public readonly record struct KeyModifiers(bool Shift, bool Alt, bool Ctrl, bool Super, bool Hyper, bool Meta, bool CapsLock, bool NumLock);

/// <summary>
/// Parsed key information from a terminal escape sequence.
/// Matches the TypeScript ParsedKey interface 1:1.
/// </summary>
public sealed class ParsedKey
{
    public required string Name { get; init; }
    public bool Ctrl { get; init; }
    public bool Meta { get; init; }
    public bool Shift { get; init; }
    public bool Option { get; init; }
    public string Sequence { get; init; } = "";
    public bool Number { get; init; }
    public string Raw { get; init; } = "";
    public KeyEventType EventType { get; init; } = KeyEventType.Press;
    public string Source { get; init; } = "raw";
    public string? Code { get; init; }
    public bool Super { get; init; }
    public bool Hyper { get; init; }
    public bool CapsLock { get; init; }
    public bool NumLock { get; init; }
    public int? BaseCode { get; init; }
    public bool Repeated { get; init; }
}

/// <summary>
/// Captured raw input sequence used by the keypress debug tooling.
/// </summary>
public sealed class DebugInputRecord
{
    public required string Timestamp { get; init; }
    public required string Sequence { get; init; }
}

/// <summary>
/// Raw mouse event parsed from terminal escape sequences.
/// Matches the TypeScript RawMouseEvent interface.
/// </summary>
public sealed class RawMouseEvent
{
    public required MouseEventType Type { get; init; }
    public int Button { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public required KeyModifiers Modifiers { get; init; }
    public ScrollInfo? Scroll { get; init; }
}

/// <summary>Kind of pasted content.</summary>
public enum PasteKind : byte
{
    Text,
    Binary,
    Unknown,
}

/// <summary>Metadata about a paste operation.</summary>
public sealed class PasteMetadata
{
    public string? MimeType { get; init; }
    public PasteKind Kind { get; init; } = PasteKind.Unknown;
}

/// <summary>
/// Discriminated union for events produced by the stdin parser.
/// </summary>
public abstract class StdinEvent
{
    private StdinEvent() { }

    public sealed class Key(string raw, ParsedKey parsedKey) : StdinEvent
    {
        public string Raw { get; } = raw;
        public ParsedKey ParsedKey { get; } = parsedKey;
    }

    public sealed class Mouse(string raw, MouseEncoding encoding, RawMouseEvent mouseEvent) : StdinEvent
    {
        public string Raw { get; } = raw;
        public MouseEncoding Encoding { get; } = encoding;
        public RawMouseEvent MouseEvent { get; } = mouseEvent;
    }

    public sealed class Paste(byte[] bytes, PasteMetadata? metadata = null) : StdinEvent
    {
        public byte[] Bytes { get; } = bytes;
        public PasteMetadata? Metadata { get; } = metadata;
    }

    public sealed class Response(string protocol, string sequence) : StdinEvent
    {
        public string Protocol { get; } = protocol;
        public string Sequence { get; } = sequence;
    }
}
