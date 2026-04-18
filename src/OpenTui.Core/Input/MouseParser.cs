namespace OpenTui.Core;

/// <summary>
/// Stateful mouse event parser. Parses SGR and X10/Basic mouse escape sequences.
/// Port of TypeScript MouseParser from parse.mouse.ts.
/// </summary>
public sealed class MouseParser
{
    private readonly HashSet<int> _mouseButtonsPressed = [];

    private static readonly string[] ScrollDirections = ["up", "down", "left", "right"];

    /// <summary>Clears tracked button state.</summary>
    public void Reset() => _mouseButtonsPressed.Clear();

    /// <summary>Parses a single mouse event from raw terminal data.</summary>
    public RawMouseEvent? ParseMouseEvent(ReadOnlySpan<byte> data)
    {
        var str = DecodeLatin1(data);
        var parsed = ParseMouseSequenceAt(str, 0);
        return parsed?.Event;
    }

    /// <summary>Parses all consecutive mouse events from raw terminal data.</summary>
    public List<RawMouseEvent> ParseAllMouseEvents(ReadOnlySpan<byte> data)
    {
        var str = DecodeLatin1(data);
        var events = new List<RawMouseEvent>();
        int offset = 0;

        while (offset < str.Length)
        {
            var parsed = ParseMouseSequenceAt(str, offset);
            if (parsed is null) break;

            events.Add(parsed.Event);
            offset += parsed.Consumed;
        }

        return events;
    }

    private static string DecodeLatin1(ReadOnlySpan<byte> data)
    {
        return System.Text.Encoding.Latin1.GetString(data);
    }

    private ParsedMouseSequence? ParseMouseSequenceAt(string str, int offset)
    {
        if (offset + 2 >= str.Length) return null;
        if (str[offset] != '\x1b' || str[offset + 1] != '[') return null;

        char introducer = str[offset + 2];

        if (introducer == '<')
            return ParseSgrSequence(str, offset);

        if (introducer == 'M')
            return ParseBasicSequence(str, offset);

        return null;
    }

    private ParsedMouseSequence? ParseSgrSequence(string str, int offset)
    {
        int index = offset + 3;
        Span<int> values = [0, 0, 0];
        int part = 0;
        bool hasDigit = false;

        while (index < str.Length)
        {
            char ch = str[index];

            if (ch >= '0' && ch <= '9')
            {
                hasDigit = true;
                values[part] = values[part] * 10 + (ch - '0');
                index++;
                continue;
            }

            switch (ch)
            {
                case ';':
                    if (!hasDigit || part >= 2) return null;
                    part++;
                    hasDigit = false;
                    index++;
                    break;

                case 'M':
                case 'm':
                    if (!hasDigit || part != 2) return null;
                    return new ParsedMouseSequence(
                        DecodeSgrEvent(values[0], values[1], values[2], ch),
                        index - offset + 1);

                default:
                    return null;
            }
        }

        return null;
    }

    private ParsedMouseSequence? ParseBasicSequence(string str, int offset)
    {
        // ESC [ M + 3 bytes
        if (offset + 6 > str.Length) return null;

        int buttonByte = str[offset + 3] - 32;
        int x = str[offset + 4] - 33;
        int y = str[offset + 5] - 33;

        return new ParsedMouseSequence(DecodeBasicEvent(buttonByte, x, y), 6);
    }

    private RawMouseEvent DecodeSgrEvent(int rawButtonCode, int wireX, int wireY, char pressRelease)
    {
        int button = rawButtonCode & 3;
        bool isScroll = (rawButtonCode & 64) != 0;
        string? scrollDirection = isScroll && button < ScrollDirections.Length ? ScrollDirections[button] : null;

        bool isMotion = (rawButtonCode & 32) != 0;
        var modifiers = new KeyModifiers(
            Shift: (rawButtonCode & 4) != 0,
            Alt: (rawButtonCode & 8) != 0,
            Ctrl: (rawButtonCode & 16) != 0,
            Super: false, Hyper: false, Meta: false, CapsLock: false, NumLock: false);

        MouseEventType type;
        ScrollInfo? scrollInfo = null;

        if (isMotion)
        {
            bool isDragging = _mouseButtonsPressed.Count > 0;
            type = (button == 3 || !isDragging) ? MouseEventType.Move : MouseEventType.Drag;
        }
        else if (isScroll && pressRelease == 'M')
        {
            type = MouseEventType.Scroll;
            scrollInfo = new ScrollInfo(scrollDirection!, 1);
        }
        else
        {
            type = pressRelease == 'M' ? MouseEventType.Down : MouseEventType.Up;

            if (type == MouseEventType.Down && button != 3)
                _mouseButtonsPressed.Add(button);
            else if (type == MouseEventType.Up)
                _mouseButtonsPressed.Clear();
        }

        return new RawMouseEvent
        {
            Type = type,
            Button = button == 3 ? 0 : button,
            X = wireX - 1,
            Y = wireY - 1,
            Modifiers = modifiers,
            Scroll = scrollInfo,
        };
    }

    private static RawMouseEvent DecodeBasicEvent(int buttonByte, int x, int y)
    {
        int button = buttonByte & 3;
        bool isScroll = (buttonByte & 64) != 0;
        bool isMotion = (buttonByte & 32) != 0;
        string? scrollDirection = isScroll && button < ScrollDirections.Length ? ScrollDirections[button] : null;

        var modifiers = new KeyModifiers(
            Shift: (buttonByte & 4) != 0,
            Alt: (buttonByte & 8) != 0,
            Ctrl: (buttonByte & 16) != 0,
            Super: false, Hyper: false, Meta: false, CapsLock: false, NumLock: false);

        MouseEventType type;
        int actualButton;
        ScrollInfo? scrollInfo = null;

        if (isMotion)
        {
            type = MouseEventType.Move;
            actualButton = button == 3 ? -1 : button;
        }
        else if (isScroll)
        {
            type = MouseEventType.Scroll;
            actualButton = 0;
            scrollInfo = new ScrollInfo(scrollDirection!, 1);
        }
        else
        {
            type = button == 3 ? MouseEventType.Up : MouseEventType.Down;
            actualButton = button == 3 ? 0 : button;
        }

        return new RawMouseEvent
        {
            Type = type,
            Button = actualButton,
            X = x,
            Y = y,
            Modifiers = modifiers,
            Scroll = scrollInfo,
        };
    }

    private sealed record ParsedMouseSequence(RawMouseEvent Event, int Consumed);
}
