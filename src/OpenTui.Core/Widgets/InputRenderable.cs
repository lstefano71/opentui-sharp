using System.Text.RegularExpressions;

namespace OpenTui.Core;

/// <summary>
/// Options for <see cref="InputRenderable"/>, a single-line editor-backed input control.
/// </summary>
public class InputOptions : TextareaOptions
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? Value { get; init; }
    /// <summary>
    /// Gets or sets the max length.
    /// </summary>
    public int MaxLength { get; init; } = 1000;
}

/// <summary>
/// Single-line text input with built-in editing, max-length enforcement, and submit/change events.
/// It is backed by the same editor stack as <see cref="TextareaRenderable"/>, but forces single-line
/// behavior and emits higher-level input-specific events.
/// </summary>
public partial class InputRenderable : TextareaRenderable
{
    /// <summary>Event-name constants emitted by <see cref="InputRenderable"/>.</summary>
    public static new class Events
    {
        /// <summary>Raised whenever the current value changes during editing.</summary>
        public const string Input = "input";
        /// <summary>Raised when a submitted value differs from the previous committed value.</summary>
        public const string Change = "change";
        /// <summary>Raised when the user submits the current value.</summary>
        public const string Enter = "enter";
    }

    private int _maxLength;
    private string _lastCommittedValue = "";

    /// <summary>
    /// Initializes a new instance of the InputRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
    public InputRenderable(IRenderContext ctx, InputOptions? options = null)
        : base(ctx, BuildBaseOptions(options))
    {
        options ??= new InputOptions();
        _maxLength = options.MaxLength;
        _lastCommittedValue = PlainText;

        string initialValue = StripNewlines(options.Value ?? "");
        if (initialValue.Length > _maxLength)
            initialValue = initialValue[.._maxLength];
        if (initialValue.Length > 0)
            EditBuffer.SetCursorByOffset((uint)System.Text.Encoding.UTF8.GetByteCount(initialValue));
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
            Width = options.Width,
            Id = options.Id,
            ZIndex = options.ZIndex,
            Visible = options.Visible,
            Buffered = options.Buffered,
            Live = options.Live,
            Opacity = options.Opacity,
            EnableLayout = options.EnableLayout,
            RenderBefore = options.RenderBefore,
            RenderAfter = options.RenderAfter,
            OnMouse = options.OnMouse,
            OnMouseDown = options.OnMouseDown,
            OnMouseUp = options.OnMouseUp,
            OnMouseMove = options.OnMouseMove,
            OnMouseDrag = options.OnMouseDrag,
            OnMouseDragEnd = options.OnMouseDragEnd,
            OnMouseDrop = options.OnMouseDrop,
            OnMouseOver = options.OnMouseOver,
            OnMouseOut = options.OnMouseOut,
            OnMouseScroll = options.OnMouseScroll,
            OnPaste = options.OnPaste,
            OnKeyDown = options.OnKeyDown,
            OnSizeChange = options.OnSizeChange,
            MinWidth = options.MinWidth,
            MinHeight = options.MinHeight,
            MaxWidth = options.MaxWidth,
            MaxHeight = options.MaxHeight,
            FlexGrow = options.FlexGrow,
            FlexShrink = options.FlexShrink,
            FlexBasis = options.FlexBasis,
            FlexDirection = options.FlexDirection,
            FlexWrap = options.FlexWrap,
            AlignItems = options.AlignItems,
            JustifyContent = options.JustifyContent,
            AlignSelf = options.AlignSelf,
            Position = options.Position,
            Overflow = options.Overflow,
            Top = options.Top,
            Right = options.Right,
            Bottom = options.Bottom,
            Left = options.Left,
            Margin = options.Margin,
            MarginX = options.MarginX,
            MarginY = options.MarginY,
            MarginTop = options.MarginTop,
            MarginRight = options.MarginRight,
            MarginBottom = options.MarginBottom,
            MarginLeft = options.MarginLeft,
            Padding = options.Padding,
            PaddingX = options.PaddingX,
            PaddingY = options.PaddingY,
            PaddingTop = options.PaddingTop,
            PaddingRight = options.PaddingRight,
            PaddingBottom = options.PaddingBottom,
            PaddingLeft = options.PaddingLeft,
            Gap = options.Gap,
            RowGap = options.RowGap,
            ColumnGap = options.ColumnGap,
            InitialValue = sanitized,
            Placeholder = options.Placeholder ?? "",
            PlaceholderColor = options.PlaceholderColor,
            BackgroundColor = options.BackgroundColor,
            TextColor = options.TextColor,
            FocusedBackgroundColor = options.FocusedBackgroundColor,
            FocusedTextColor = options.FocusedTextColor,
            OnSubmit = options.OnSubmit,
            SelectionBg = options.SelectionBg,
            SelectionFg = options.SelectionFg,
            Selectable = options.Selectable,
            Attributes = options.Attributes,
            ScrollMargin = options.ScrollMargin,
            ScrollSpeed = options.ScrollSpeed,
            ShowCursor = options.ShowCursor,
            CursorColor = options.CursorColor,
            CursorStyle = options.CursorStyle,
            SyntaxStyle = options.SyntaxStyle,
            OnCursorChange = options.OnCursorChange,
            OnContentChange = options.OnContentChange,
            Height = DimensionValue.Point(1),
            WrapMode = 0, // 0 = none
        };
    }

    #region Properties

    /// <summary>Gets or sets the current single-line value.</summary>
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
                EditBuffer.SetCursorByOffset((uint)System.Text.Encoding.UTF8.GetByteCount(newValue));
                Emit<string>(Events.Input, newValue);
            }
        }
    }

    /// <summary>Gets or sets the maximum allowed character count.</summary>
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

    /// <inheritdoc />
    public override bool NewLine() => false; // No newlines in single-line input

    /// <inheritdoc />
    public override bool Submit()
    {
        string currentValue = PlainText;
        if (currentValue != _lastCommittedValue)
        {
            _lastCommittedValue = currentValue;
            Emit<string>(Events.Change, currentValue);
        }

        base.Submit();
        Emit<string>(Events.Enter, currentValue);
        return true;
    }

    /// <inheritdoc />
    public override void InsertText(string text)
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

    /// <inheritdoc />
    public override bool DeleteCharBackward()
    {
        bool result = base.DeleteCharBackward();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    /// <inheritdoc />
    public override bool DeleteChar()
    {
        bool result = base.DeleteChar();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    /// <inheritdoc />
    public override bool DeleteLine()
    {
        bool result = base.DeleteLine();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    /// <inheritdoc />
    public override bool DeleteWordForward()
    {
        bool result = base.DeleteWordForward();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    /// <inheritdoc />
    public override bool DeleteWordBackward()
    {
        bool result = base.DeleteWordBackward();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    /// <inheritdoc />
    public override bool DeleteToLineStart()
    {
        bool result = base.DeleteToLineStart();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    /// <inheritdoc />
    public override bool DeleteToLineEnd()
    {
        bool result = base.DeleteToLineEnd();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    /// <inheritdoc />
    public override bool Undo()
    {
        bool result = base.Undo();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    /// <inheritdoc />
    public override bool Redo()
    {
        bool result = base.Redo();
        Emit<string>(Events.Input, PlainText);
        return result;
    }

    /// <inheritdoc />
    public override void Focus()
    {
        base.Focus();
        _lastCommittedValue = PlainText;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    protected override void HandleKeyPress(KeyEvent key)
    {
        if (key.Name is "return" or "linefeed")
        {
            Submit();
            key.StopPropagation();
            return;
        }

        base.HandleKeyPress(key);
    }

    #endregion

    private static string StripNewlines(string text) =>
        NewlineRegex().Replace(text, "");

    [GeneratedRegex(@"[\n\r]")]
    private static partial Regex NewlineRegex();
}
