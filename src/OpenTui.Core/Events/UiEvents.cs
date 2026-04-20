namespace OpenTui.Core;

/// <summary>
/// Base class for DOM-style events with stopPropagation/preventDefault.
/// </summary>
public abstract class UiEvent
{
    /// <summary>
    /// Gets or sets a value indicating whether is propagation stopped.
    /// </summary>
    public bool IsPropagationStopped { get; private set; }
    /// <summary>
    /// Gets or sets a value indicating whether is default prevented.
    /// </summary>
    public bool IsDefaultPrevented { get; private set; }

    /// <summary>
    /// Performs stop propagation.
    /// </summary>
    public void StopPropagation() => IsPropagationStopped = true;
    /// <summary>
    /// Performs prevent default.
    /// </summary>
    public void PreventDefault() => IsDefaultPrevented = true;
}

/// <summary>
/// Keyboard event wrapping a ParsedKey, with DOM-style propagation control.
/// Matches TypeScript KeyEvent from KeyHandler.ts.
/// </summary>
public sealed class KeyEvent(ParsedKey key) : UiEvent
{
    /// <summary>
    /// Gets the key.
    /// </summary>
    public ParsedKey Key { get; } = key;
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name => Key.Name;
    /// <summary>
    /// Gets the ctrl.
    /// </summary>
    public bool Ctrl => Key.Ctrl;
    /// <summary>
    /// Gets the meta.
    /// </summary>
    public bool Meta => Key.Meta;
    /// <summary>
    /// Gets the shift.
    /// </summary>
    public bool Shift => Key.Shift;
    /// <summary>
    /// Gets the option.
    /// </summary>
    public bool Option => Key.Option;
    /// <summary>
    /// Gets the super.
    /// </summary>
    public bool Super => Key.Super;
    /// <summary>
    /// Gets the hyper.
    /// </summary>
    public bool Hyper => Key.Hyper;
    /// <summary>
    /// Gets the sequence.
    /// </summary>
    public string Sequence => Key.Sequence;
    /// <summary>
    /// Gets the number.
    /// </summary>
    public bool Number => Key.Number;
    /// <summary>
    /// Gets the raw.
    /// </summary>
    public string Raw => Key.Raw;
    /// <summary>
    /// Gets the event type.
    /// </summary>
    public KeyEventType EventType => Key.EventType;
    /// <summary>
    /// Gets the source.
    /// </summary>
    public string Source => Key.Source;
    /// <summary>
    /// Gets the code.
    /// </summary>
    public string? Code => Key.Code;
    /// <summary>
    /// Gets the caps lock.
    /// </summary>
    public bool CapsLock => Key.CapsLock;
    /// <summary>
    /// Gets the num lock.
    /// </summary>
    public bool NumLock => Key.NumLock;
    /// <summary>
    /// Gets the base code.
    /// </summary>
    public int? BaseCode => Key.BaseCode;
    /// <summary>
    /// Gets the repeated.
    /// </summary>
    public bool Repeated => Key.Repeated;
}

/// <summary>
/// Mouse event with target tracking and propagation control.
/// Matches TypeScript MouseEvent from renderer.ts.
/// </summary>
public sealed class UiMouseEvent : UiEvent
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
    public KeyModifiers Modifiers { get; init; }
    /// <summary>
    /// Gets or sets the scroll.
    /// </summary>
    public ScrollInfo? Scroll { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether is dragging.
    /// </summary>
    public bool IsDragging { get; init; }
    /// <summary>
    /// Gets or sets the target.
    /// </summary>
    public object? Target { get; init; }
    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    public string? Source { get; init; }
}

/// <summary>
/// Paste event wrapping raw bytes with metadata.
/// Matches TypeScript PasteEvent from KeyHandler.ts.
/// </summary>
public sealed class PasteEvent(byte[] bytes, PasteMetadata? metadata = null) : UiEvent
{
    /// <summary>
    /// Gets the bytes.
    /// </summary>
    public byte[] Bytes { get; } = bytes;
    /// <summary>
    /// Gets the metadata.
    /// </summary>
    public PasteMetadata? Metadata { get; } = metadata;

    /// <summary>Decodes the paste content as UTF-8 text.</summary>
    public string Text => System.Text.Encoding.UTF8.GetString(Bytes);
}
