namespace OpenTui.Core;

/// <summary>
/// Base class for DOM-style events with stopPropagation/preventDefault.
/// </summary>
public abstract class UiEvent
{
    public bool IsPropagationStopped { get; private set; }
    public bool IsDefaultPrevented { get; private set; }

    public void StopPropagation() => IsPropagationStopped = true;
    public void PreventDefault() => IsDefaultPrevented = true;
}

/// <summary>
/// Keyboard event wrapping a ParsedKey, with DOM-style propagation control.
/// Matches TypeScript KeyEvent from KeyHandler.ts.
/// </summary>
public sealed class KeyEvent(ParsedKey key) : UiEvent
{
    public ParsedKey Key { get; } = key;
    public string Name => Key.Name;
    public bool Ctrl => Key.Ctrl;
    public bool Meta => Key.Meta;
    public bool Shift => Key.Shift;
    public bool Option => Key.Option;
    public string Sequence => Key.Sequence;
    public KeyEventType EventType => Key.EventType;
}

/// <summary>
/// Mouse event with target tracking and propagation control.
/// Matches TypeScript MouseEvent from renderer.ts.
/// </summary>
public sealed class UiMouseEvent : UiEvent
{
    public required MouseEventType Type { get; init; }
    public int Button { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public KeyModifiers Modifiers { get; init; }
    public ScrollInfo? Scroll { get; init; }
    public bool IsDragging { get; init; }
    public object? Target { get; init; }
    public string? Source { get; init; }
}

/// <summary>
/// Paste event wrapping raw bytes with metadata.
/// Matches TypeScript PasteEvent from KeyHandler.ts.
/// </summary>
public sealed class PasteEvent(byte[] bytes, PasteMetadata? metadata = null) : UiEvent
{
    public byte[] Bytes { get; } = bytes;
    public PasteMetadata? Metadata { get; } = metadata;

    /// <summary>Decodes the paste content as UTF-8 text.</summary>
    public string Text => System.Text.Encoding.UTF8.GetString(Bytes);
}
