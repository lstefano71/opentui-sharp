namespace OpenTui.Core;

/// <summary>
/// Options for Textarea renderable.
/// Matches TypeScript TextareaOptions.
/// </summary>
public class TextareaOptions : EditBufferOptions
{
    public string? InitialValue { get; init; }
    public Rgba? FocusedBackgroundColor { get; init; }
    public Rgba? FocusedTextColor { get; init; }
    public string? Placeholder { get; init; }
    public Rgba? PlaceholderColor { get; init; }
    public Action? OnSubmit { get; init; }
}

/// <summary>
/// Multi-line text editor with cursor tracking, selection, undo/redo, and keybindings.
/// Matches TypeScript TextareaRenderable from Textarea.ts.
/// </summary>
public class TextareaRenderable : EditBufferRenderable
{
    public static class Events
    {
        public const string Submit = "submit";
    }

    private string? _placeholder;
    private Rgba _placeholderColor;
    private Rgba _unfocusedBackgroundColor;
    private Rgba _unfocusedTextColor;
    private Rgba _focusedBackgroundColor;
    private Rgba _focusedTextColor;
    private bool _initialValueSet;
    private Action? _submitListener;

    public TextareaRenderable(IRenderContext ctx, TextareaOptions? options = null)
        : base(ctx, options ?? new TextareaOptions())
    {
        options ??= new TextareaOptions();

        _unfocusedBackgroundColor = options.BackgroundColor ?? Rgba.Transparent;
        _unfocusedTextColor = options.TextColor ?? Rgba.FromInts(255, 255, 255);
        _focusedBackgroundColor = options.FocusedBackgroundColor ?? _unfocusedBackgroundColor;
        _focusedTextColor = options.FocusedTextColor ?? _unfocusedTextColor;
        _placeholder = options.Placeholder;
        _placeholderColor = options.PlaceholderColor ?? Rgba.FromHex("#666666");
        _submitListener = options.OnSubmit;

        if (options.InitialValue is { } initial)
        {
            SetText(initial);
            _initialValueSet = true;
        }

        UpdateColors();
        ApplyPlaceholder(_placeholder);
    }

    #region Properties

    public string? Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value;
            ApplyPlaceholder(value);
            RequestRender();
        }
    }

    public Rgba PlaceholderColor
    {
        get => _placeholderColor;
        set
        {
            _placeholderColor = value;
            ApplyPlaceholder(_placeholder);
            RequestRender();
        }
    }

    public override Rgba BackgroundColor
    {
        get => _unfocusedBackgroundColor;
        set
        {
            _unfocusedBackgroundColor = value;
            UpdateColors();
        }
    }

    public override Rgba TextColor
    {
        get => _unfocusedTextColor;
        set
        {
            _unfocusedTextColor = value;
            UpdateColors();
        }
    }

    public Rgba FocusedBackgroundColor
    {
        get => _focusedBackgroundColor;
        set { _focusedBackgroundColor = value; UpdateColors(); }
    }

    public Rgba FocusedTextColor
    {
        get => _focusedTextColor;
        set { _focusedTextColor = value; UpdateColors(); }
    }

    public string InitialValue
    {
        set
        {
            if (!_initialValueSet)
            {
                SetText(value);
                _initialValueSet = true;
            }
        }
    }

    #endregion

    #region Focus

    public override void Focus()
    {
        base.Focus();
        UpdateColors();
    }

    public override void Blur()
    {
        base.Blur();
        UpdateColors();
    }

    #endregion

    #region Keyboard

    protected override void HandleKeyPress(KeyEvent key)
    {
        bool handled = DispatchAction(key);
        if (handled)
        {
            key.StopPropagation();
            return;
        }

        // Raw character input
        if (!key.Ctrl && !key.Meta)
        {
            if (key.Name == "space")
            {
                InsertText(" ");
                key.StopPropagation();
                return;
            }

            if (key.Sequence is { } seq && seq.Length > 0)
            {
                char first = seq[0];
                if (first >= 32 && first != 127)
                {
                    InsertText(seq);
                    key.StopPropagation();
                    return;
                }
            }
        }

        base.HandleKeyPress(key);
    }

    private bool DispatchAction(KeyEvent key)
    {
        // Default textarea keybindings matching TS
        return key switch
        {
            { Name: "left", Shift: false, Ctrl: false, Meta: false } => MoveCursorLeft(),
            { Name: "right", Shift: false, Ctrl: false, Meta: false } => MoveCursorRight(),
            { Name: "up", Shift: false, Ctrl: false, Meta: false } => MoveCursorUp(),
            { Name: "down", Shift: false, Ctrl: false, Meta: false } => MoveCursorDown(),
            { Name: "left", Shift: true, Ctrl: false } => MoveCursorLeft(select: true),
            { Name: "right", Shift: true, Ctrl: false } => MoveCursorRight(select: true),
            { Name: "up", Shift: true, Ctrl: false } => MoveCursorUp(select: true),
            { Name: "down", Shift: true, Ctrl: false } => MoveCursorDown(select: true),

            { Name: "home" } when !key.Shift => GotoBufferHome(),
            { Name: "end" } when !key.Shift => GotoBufferEnd(),
            { Name: "home", Shift: true } => GotoBufferHome(select: true),
            { Name: "end", Shift: true } => GotoBufferEnd(select: true),

            { Name: "a", Ctrl: true } when !key.Shift => GotoLineHome(),
            { Name: "e", Ctrl: true } when !key.Shift => GotoLineEnd(),
            { Name: "a", Ctrl: true, Shift: true } => GotoLineHome(select: true),
            { Name: "e", Ctrl: true, Shift: true } => GotoLineEnd(select: true),

            { Name: "backspace" } when !key.Ctrl && !key.Meta => DeleteCharBackward(),
            { Name: "delete" } when !key.Ctrl && !key.Meta => DeleteChar(),
            { Name: "return" or "linefeed" } when !key.Meta => NewLine(),
            { Name: "return", Meta: true } => Submit(),

            // Word movement
            { Name: "right", Ctrl: true } when !key.Shift => MoveWordForward(),
            { Name: "left", Ctrl: true } when !key.Shift => MoveWordBackward(),
            { Name: "right", Ctrl: true, Shift: true } => MoveWordForward(select: true),
            { Name: "left", Ctrl: true, Shift: true } => MoveWordBackward(select: true),

            // Delete word
            { Name: "w", Ctrl: true } => DeleteWordBackward(),
            { Name: "backspace", Ctrl: true } => DeleteWordBackward(),
            { Name: "backspace", Meta: true } => DeleteWordBackward(),
            { Name: "delete", Ctrl: true } => DeleteWordForward(),

            // Line operations
            { Name: "d", Ctrl: true, Shift: true } => DeleteLine(),
            { Name: "k", Ctrl: true } => DeleteToLineEnd(),
            { Name: "u", Ctrl: true } => DeleteToLineStart(),

            // Undo/redo
            { Name: "-", Ctrl: true } => Undo(),
            { Name: ".", Ctrl: true } => Redo(),

            _ => false,
        };
    }

    public bool Submit()
    {
        _submitListener?.Invoke();
        Emit(Events.Submit);
        return true;
    }

    #endregion

    #region Internal

    private void UpdateColors()
    {
        var effectiveBg = Focused ? _focusedBackgroundColor : _unfocusedBackgroundColor;
        var effectiveFg = Focused ? _focusedTextColor : _unfocusedTextColor;
        base.BackgroundColor = effectiveBg;
        base.TextColor = effectiveFg;
    }

    private void ApplyPlaceholder(string? placeholder)
    {
        if (string.IsNullOrEmpty(placeholder))
        {
            EditorView.SetPlaceholderStyledText([]);
            return;
        }

        // Build minimal styled text data — for now just plain UTF-8
        // The native format expects serialized styled text; plain bytes may
        // suffice for simple placeholder text until full serialization is implemented.
        var placeholderBytes = System.Text.Encoding.UTF8.GetBytes(placeholder);
        EditorView.SetPlaceholderStyledText(placeholderBytes);
    }

    #endregion
}
