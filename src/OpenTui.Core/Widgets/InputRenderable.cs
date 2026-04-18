using System.Text.RegularExpressions;

namespace OpenTui.Core;

/// <summary>
/// Options for Input renderable.
/// Matches TypeScript InputRenderableOptions.
/// </summary>
public class InputOptions : TextareaOptions
{
    public string? Value { get; init; }
    public int MaxLength { get; init; } = 1000;
}

/// <summary>
/// Single-line text input with maxLength, newline stripping, and input/change events.
/// Extends TextareaRenderable with height=1, wrapMode="none", return→submit.
/// Matches TypeScript InputRenderable from Input.ts.
/// </summary>
public partial class InputRenderable : TextareaRenderable
{
    public static new class Events
    {
        public const string Input = "input";
        public const string Change = "change";
        public const string Enter = "enter";
    }

    private int _maxLength;
    private string _lastCommittedValue = "";

    public InputRenderable(IRenderContext ctx, InputOptions? options = null)
        : base(ctx, BuildBaseOptions(options))
    {
        options ??= new InputOptions();
        _maxLength = options.MaxLength;
        _lastCommittedValue = PlainText;
    }

    private static TextareaOptions BuildBaseOptions(InputOptions? options)
    {
        options ??= new InputOptions();
        string rawValue = options.Value ?? "";
        string sanitized = StripNewlines(rawValue);
        if (sanitized.Length > options.MaxLength)
            sanitized = sanitized[..options.MaxLength];

        return new TextareaOptions
        {
            Id = options.Id,
            Buffered = options.Buffered,
            InitialValue = sanitized,
            Placeholder = options.Placeholder ?? "",
            PlaceholderColor = options.PlaceholderColor,
            BackgroundColor = options.BackgroundColor,
            TextColor = options.TextColor,
            FocusedBackgroundColor = options.FocusedBackgroundColor,
            FocusedTextColor = options.FocusedTextColor,
            OnSubmit = options.OnSubmit,
            Height = DimensionValue.Point(1),
            WrapMode = 0, // 0 = none
        };
    }

    #region Properties

    public string Value
    {
        get => PlainText;
        set
        {
            string newValue = StripNewlines(value);
            if (newValue.Length > _maxLength)
                newValue = newValue[.._maxLength];
            if (PlainText != newValue)
            {
                SetText(newValue);
                Emit<string>(Events.Input, newValue);
            }
        }
    }

    public int MaxLength
    {
        get => _maxLength;
        set
        {
            _maxLength = value;
            if (PlainText.Length > value)
                SetText(PlainText[..value]);
        }
    }

    #endregion

    #region Overrides

    public new bool NewLine() => false; // No newlines in single-line input

    public new void InsertText(string text)
    {
        string sanitized = StripNewlines(text);
        if (string.IsNullOrEmpty(sanitized)) return;

        int remaining = _maxLength - PlainText.Length;
        if (remaining <= 0) return;

        if (sanitized.Length > remaining)
            sanitized = sanitized[..remaining];

        base.InsertText(sanitized);
        Emit<string>(Events.Input, PlainText);
    }

    public new bool DeleteCharBackward()
    {
        bool result = base.DeleteCharBackward();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    public new bool DeleteChar()
    {
        bool result = base.DeleteChar();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    public new bool DeleteLine()
    {
        bool result = base.DeleteLine();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    public new bool DeleteWordForward()
    {
        bool result = base.DeleteWordForward();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    public new bool DeleteWordBackward()
    {
        bool result = base.DeleteWordBackward();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    public new bool DeleteToLineStart()
    {
        bool result = base.DeleteToLineStart();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    public new bool DeleteToLineEnd()
    {
        bool result = base.DeleteToLineEnd();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    public new bool Undo()
    {
        bool result = base.Undo();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    public new bool Redo()
    {
        bool result = base.Redo();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    public override void Focus()
    {
        base.Focus();
        _lastCommittedValue = PlainText;
    }

    public override void Blur()
    {
        string currentValue = PlainText;
        if (currentValue != _lastCommittedValue)
        {
            _lastCommittedValue = currentValue;
            Emit<string>(Events.Change, currentValue);
        }
        base.Blur();
    }

    #endregion

    private static string StripNewlines(string text) =>
        NewlineRegex().Replace(text, "");

    [GeneratedRegex(@"[\n\r]")]
    private static partial Regex NewlineRegex();
}
