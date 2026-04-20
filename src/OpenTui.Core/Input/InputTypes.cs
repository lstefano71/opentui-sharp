namespace OpenTui.Core;

/// <summary>Type of key event in the Kitty keyboard protocol.</summary>
public enum KeyEventType : byte
{
    /// <summary>
    /// Represents the Press option.
    /// </summary>
    Press = 0,
    /// <summary>
    /// Represents the Repeat option.
    /// </summary>
    Repeat = 1,
    /// <summary>
    /// Represents the Release option.
    /// </summary>
    Release = 2,
}

/// <summary>Type of mouse interaction.</summary>
public enum MouseEventType : byte
{
    /// <summary>
    /// Represents the Down option.
    /// </summary>
    Down,
    /// <summary>
    /// Represents the Up option.
    /// </summary>
    Up,
    /// <summary>
    /// Represents the Move option.
    /// </summary>
    Move,
    /// <summary>
    /// Represents the Drag option.
    /// </summary>
    Drag,
    /// <summary>
    /// Represents the Drag End option.
    /// </summary>
    DragEnd,
    /// <summary>
    /// Represents the Drop option.
    /// </summary>
    Drop,
    /// <summary>
    /// Represents the Over option.
    /// </summary>
    Over,
    /// <summary>
    /// Represents the Out option.
    /// </summary>
    Out,
    /// <summary>
    /// Represents the Scroll option.
    /// </summary>
    Scroll,
}

/// <summary>Mouse button constants.</summary>
public enum MouseButton : byte
{
    /// <summary>
    /// Represents the Left option.
    /// </summary>
    Left = 0,
    /// <summary>
    /// Represents the Middle option.
    /// </summary>
    Middle = 1,
    /// <summary>
    /// Represents the Right option.
    /// </summary>
    Right = 2,
    /// <summary>
    /// Represents the Wheel Up option.
    /// </summary>
    WheelUp = 4,
    /// <summary>
    /// Represents the Wheel Down option.
    /// </summary>
    WheelDown = 5,
    /// <summary>
    /// Represents the Wheel Left option.
    /// </summary>
    WheelLeft = 6,
    /// <summary>
    /// Represents the Wheel Right option.
    /// </summary>
    WheelRight = 7,
}

/// <summary>Mouse encoding format on the wire.</summary>
public enum MouseEncoding : byte
{
    /// <summary>
    /// Represents the Sgr option.
    /// </summary>
    Sgr,
    /// <summary>
    /// Represents the X 10 option.
    /// </summary>
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
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the ctrl.
    /// </summary>
    public bool Ctrl { get; init; }
    /// <summary>
    /// Gets or sets the meta.
    /// </summary>
    public bool Meta { get; init; }
    /// <summary>
    /// Gets or sets the shift.
    /// </summary>
    public bool Shift { get; init; }
    /// <summary>
    /// Gets or sets the option.
    /// </summary>
    public bool Option { get; init; }
    /// <summary>
    /// Gets or sets the sequence.
    /// </summary>
    public string Sequence { get; init; } = "";
    /// <summary>
    /// Gets or sets the number.
    /// </summary>
    public bool Number { get; init; }
    /// <summary>
    /// Gets or sets the raw.
    /// </summary>
    public string Raw { get; init; } = "";
    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public KeyEventType EventType { get; init; } = KeyEventType.Press;
    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    public string Source { get; init; } = "raw";
    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string? Code { get; init; }
    /// <summary>
    /// Gets or sets the super.
    /// </summary>
    public bool Super { get; init; }
    /// <summary>
    /// Gets or sets the hyper.
    /// </summary>
    public bool Hyper { get; init; }
    /// <summary>
    /// Gets or sets the caps lock.
    /// </summary>
    public bool CapsLock { get; init; }
    /// <summary>
    /// Gets or sets the num lock.
    /// </summary>
    public bool NumLock { get; init; }
    /// <summary>
    /// Gets or sets the base code.
    /// </summary>
    public int? BaseCode { get; init; }
    /// <summary>
    /// Gets or sets the repeated.
    /// </summary>
    public bool Repeated { get; init; }
}

/// <summary>
/// Captured raw input sequence used by the keypress debug tooling.
/// </summary>
public sealed class DebugInputRecord
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public required string Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the sequence.
    /// </summary>
    public required string Sequence { get; init; }
}

/// <summary>
/// Raw mouse event parsed from terminal escape sequences.
/// Matches the TypeScript RawMouseEvent interface.
/// </summary>
public sealed class RawMouseEvent
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public required MouseEventType Type { get; init; }
    /// <summary>
    /// Gets or sets the button.
    /// </summary>
    public int Button { get; init; }
    /// <summary>
    /// Gets or sets the x.
    /// </summary>
    public int X { get; init; }
    /// <summary>
    /// Gets or sets the y.
    /// </summary>
    public int Y { get; init; }
    /// <summary>
    /// Gets or sets the modifiers.
    /// </summary>
    public required KeyModifiers Modifiers { get; init; }
    /// <summary>
    /// Gets or sets the scroll.
    /// </summary>
    public ScrollInfo? Scroll { get; init; }
}

/// <summary>Kind of pasted content.</summary>
public enum PasteKind : byte
{
    /// <summary>
    /// Represents the Text option.
    /// </summary>
    Text,
    /// <summary>
    /// Represents the Binary option.
    /// </summary>
    Binary,
    /// <summary>
    /// Represents the Unknown option.
    /// </summary>
    Unknown,
}

/// <summary>Metadata about a paste operation.</summary>
public sealed class PasteMetadata
{
    /// <summary>
    /// Gets or sets the mime type.
    /// </summary>
    public string? MimeType { get; init; }
    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public PasteKind Kind { get; init; } = PasteKind.Unknown;
}

/// <summary>
/// Discriminated union for events produced by the stdin parser.
/// </summary>
public abstract class StdinEvent
{
    private StdinEvent() { }

    /// <summary>
    /// Represents a Key.
    /// </summary>
    public sealed class Key(string raw, ParsedKey parsedKey) : StdinEvent
    {
        /// <summary>
        /// Gets the raw.
        /// </summary>
        public string Raw { get; } = raw;
        /// <summary>
        /// Gets the parsed key.
        /// </summary>
        public ParsedKey ParsedKey { get; } = parsedKey;
    }

    /// <summary>
    /// Represents a Mouse.
    /// </summary>
    public sealed class Mouse(string raw, MouseEncoding encoding, RawMouseEvent mouseEvent) : StdinEvent
    {
        /// <summary>
        /// Gets the raw.
        /// </summary>
        public string Raw { get; } = raw;
        /// <summary>
        /// Gets the encoding.
        /// </summary>
        public MouseEncoding Encoding { get; } = encoding;
        /// <summary>
        /// Gets the mouse event.
        /// </summary>
        public RawMouseEvent MouseEvent { get; } = mouseEvent;
    }

    /// <summary>
    /// Represents a Paste.
    /// </summary>
    public sealed class Paste(byte[] bytes, PasteMetadata? metadata = null) : StdinEvent
    {
        /// <summary>
        /// Gets the bytes.
        /// </summary>
        public byte[] Bytes { get; } = bytes;
        /// <summary>
        /// Gets the metadata.
        /// </summary>
        public PasteMetadata? Metadata { get; } = metadata;
    }

    /// <summary>
    /// Represents a Response.
    /// </summary>
    public sealed class Response(string protocol, string sequence) : StdinEvent
    {
        /// <summary>
        /// Gets the protocol.
        /// </summary>
        public string Protocol { get; } = protocol;
        /// <summary>
        /// Gets the sequence.
        /// </summary>
        public string Sequence { get; } = sequence;
    }
}
