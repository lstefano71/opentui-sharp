using System.Runtime.InteropServices;
using System.Text;
using OpenTui.Core.Native;

namespace OpenTui.Core;

/// <summary>
/// Options for <see cref="TextareaRenderable"/>, the multi-line editor-backed text input control.
/// </summary>
public class TextareaOptions : EditBufferOptions
{
    /// <summary>
    /// Gets or sets the initial value.
    /// </summary>
    public string? InitialValue { get; init; }
    /// <summary>
    /// Gets or sets the focused background color.
    /// </summary>
    public Rgba? FocusedBackgroundColor { get; init; }
    /// <summary>
    /// Gets or sets the focused text color.
    /// </summary>
    public Rgba? FocusedTextColor { get; init; }
    /// <summary>
    /// Gets or sets the placeholder.
    /// </summary>
    public string? Placeholder { get; init; }
    /// <summary>
    /// Gets or sets the placeholder color.
    /// </summary>
    public Rgba? PlaceholderColor { get; init; }
    /// <summary>
    /// Gets or sets the on submit.
    /// </summary>
    public Action? OnSubmit { get; init; }
}

/// <summary>
/// Multi-line text editor with cursor tracking, selection, undo/redo, and keyboard command handling.
/// </summary>
public class TextareaRenderable : EditBufferRenderable
{
    /// <summary>Event-name constants emitted by <see cref="TextareaRenderable"/>.</summary>
    public static class Events
    {
        /// <summary>Raised when the editor is submitted.</summary>
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

    /// <summary>
    /// Initializes a new instance of the TextareaRenderable class.
    /// </summary>
    /// <param name="ctx">The render context.</param>
    /// <param name="options">The configuration options.</param>
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

    /// <summary>Gets or sets placeholder text shown when the editor is empty.</summary>
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

    /// <summary>Gets or sets the color used for placeholder text.</summary>
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

    /// <inheritdoc />
    public override Rgba BackgroundColor
    {
        get => _unfocusedBackgroundColor;
        set
        {
            _unfocusedBackgroundColor = value;
            UpdateColors();
        }
    }

    /// <inheritdoc />
    public override Rgba TextColor
    {
        get => _unfocusedTextColor;
        set
        {
            _unfocusedTextColor = value;
            UpdateColors();
        }
    }

    /// <summary>Gets or sets the background color used while the editor is focused.</summary>
    public Rgba FocusedBackgroundColor
    {
        get => _focusedBackgroundColor;
        set
        {
            _focusedBackgroundColor = value;
            UpdateColors();
        }
    }

    /// <summary>Gets or sets the text color used while the editor is focused.</summary>
    public Rgba FocusedTextColor
    {
        get => _focusedTextColor;
        set
        {
            _focusedTextColor = value;
            UpdateColors();
        }
    }

    /// <summary>Sets the initial editor value the first time it is assigned.</summary>
    public string InitialValue
    {
        set
        {
            if (_initialValueSet) return;
            SetText(value);
            _initialValueSet = true;
        }
    }

    #endregion

    #region Focus

    /// <inheritdoc />
    public override void Focus()
    {
        base.Focus();
        UpdateColors();
    }

    /// <inheritdoc />
    public override void Blur()
    {
        base.Blur();
        UpdateColors();
    }

    #endregion

    #region Keyboard

    /// <inheritdoc />
    protected override void HandleKeyPress(KeyEvent key)
    {
        bool handled = DispatchAction(key);
        if (handled)
        {
            key.StopPropagation();
            return;
        }

        if (!key.Ctrl && !key.Meta && !key.Super && !key.Hyper)
        {
            if (key.Name == "space")
            {
                InsertText(" ");
                key.StopPropagation();
                return;
            }

            if (key.Sequence is { Length: > 0 } seq)
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
        bool metaLike = key.Meta || key.Option;

        return key switch
        {
            { Name: "left", Shift: false, Ctrl: false, Meta: false, Option: false, Super: false } => MoveCursorLeft(),
            { Name: "right", Shift: false, Ctrl: false, Meta: false, Option: false, Super: false } => MoveCursorRight(),
            { Name: "up", Shift: false, Ctrl: false, Meta: false, Option: false, Super: false } => MoveCursorUp(),
            { Name: "down", Shift: false, Ctrl: false, Meta: false, Option: false, Super: false } => MoveCursorDown(),
            { Name: "left", Shift: true, Ctrl: false, Super: false } when !metaLike => MoveCursorLeft(select: true),
            { Name: "right", Shift: true, Ctrl: false, Super: false } when !metaLike => MoveCursorRight(select: true),
            { Name: "up", Shift: true, Ctrl: false, Super: false } when !metaLike => MoveCursorUp(select: true),
            { Name: "down", Shift: true, Ctrl: false, Super: false } when !metaLike => MoveCursorDown(select: true),

            { Name: "home" } when !key.Shift => GotoBufferHome(),
            { Name: "end" } when !key.Shift => GotoBufferEnd(),
            { Name: "home", Shift: true } => GotoBufferHome(select: true),
            { Name: "end", Shift: true } => GotoBufferEnd(select: true),

            { Name: "a", Ctrl: true } when !key.Shift => GotoLineHome(),
            { Name: "e", Ctrl: true } when !key.Shift => GotoLineEnd(),
            { Name: "a", Ctrl: true, Shift: true } => GotoLineHome(select: true),
            { Name: "e", Ctrl: true, Shift: true } => GotoLineEnd(select: true),
            { Name: "a", Shift: false, Super: false } when metaLike => GotoVisualLineHome(),
            { Name: "e", Shift: false, Super: false } when metaLike => GotoVisualLineEnd(),
            { Name: "a", Shift: true, Super: false } when metaLike => GotoVisualLineHome(select: true),
            { Name: "e", Shift: true, Super: false } when metaLike => GotoVisualLineEnd(select: true),

            { Name: "left", Super: true } when !key.Shift => GotoVisualLineHome(),
            { Name: "right", Super: true } when !key.Shift => GotoVisualLineEnd(),
            { Name: "up", Super: true } when !key.Shift => GotoBufferHome(),
            { Name: "down", Super: true } when !key.Shift => GotoBufferEnd(),
            { Name: "left", Super: true, Shift: true } => GotoVisualLineHome(select: true),
            { Name: "right", Super: true, Shift: true } => GotoVisualLineEnd(select: true),
            { Name: "up", Super: true, Shift: true } => GotoBufferHome(select: true),
            { Name: "down", Super: true, Shift: true } => GotoBufferEnd(select: true),
            { Name: "a", Super: true } => SelectAll(),

            { Name: "f", Ctrl: true } when !key.Shift => MoveCursorRight(),
            { Name: "b", Ctrl: true } when !key.Shift => MoveCursorLeft(),

            { Name: "backspace" } when !key.Ctrl && !key.Meta && !key.Option => DeleteCharBackward(),
            { Name: "backspace", Shift: true } when !key.Ctrl && !key.Meta && !key.Option => DeleteCharBackward(),
            { Name: "d", Ctrl: true } when !key.Shift => DeleteChar(),
            { Name: "delete" } when !key.Ctrl && !key.Meta && !key.Option => DeleteChar(),
            { Name: "return" or "linefeed" } when !key.Meta && !key.Option => NewLine(),
            { Name: "return" } when metaLike => Submit(),

            { Name: "f", Shift: false, Super: false } when metaLike => MoveWordForward(),
            { Name: "b", Shift: false, Super: false } when metaLike => MoveWordBackward(),
            { Name: "right", Shift: false, Super: false } when metaLike => MoveWordForward(),
            { Name: "left", Shift: false, Super: false } when metaLike => MoveWordBackward(),
            { Name: "right", Ctrl: true } when !key.Shift => MoveWordForward(),
            { Name: "left", Ctrl: true } when !key.Shift => MoveWordBackward(),
            { Name: "f", Shift: true, Super: false } when metaLike => MoveWordForward(select: true),
            { Name: "b", Shift: true, Super: false } when metaLike => MoveWordBackward(select: true),
            { Name: "right", Shift: true, Super: false } when metaLike => MoveWordForward(select: true),
            { Name: "left", Shift: true, Super: false } when metaLike => MoveWordBackward(select: true),
            { Name: "right", Ctrl: true, Shift: true } => MoveWordForward(select: true),
            { Name: "left", Ctrl: true, Shift: true } => MoveWordBackward(select: true),

            { Name: "w", Ctrl: true } => DeleteWordBackward(),
            { Name: "backspace", Ctrl: true } => DeleteWordBackward(),
            { Name: "backspace" } when metaLike => DeleteWordBackward(),
            { Name: "d" } when metaLike => DeleteWordForward(),
            { Name: "delete" } when metaLike => DeleteWordForward(),
            { Name: "delete", Ctrl: true } => DeleteWordForward(),

            { Name: "d", Ctrl: true, Shift: true } => DeleteLine(),
            { Name: "k", Ctrl: true } => DeleteToLineEnd(),
            { Name: "u", Ctrl: true } => DeleteToLineStart(),

            { Name: "-", Ctrl: true } => Undo(),
            { Name: ".", Ctrl: true } => Redo(),
            { Name: "z", Super: true } when !key.Shift => Undo(),
            { Name: "z", Super: true, Shift: true } => Redo(),

            _ => false,
        };
    }

    /// <summary>
    /// Performs submit.
    /// </summary>
    /// <returns>true if submit; otherwise, false.</returns>
    public virtual bool Submit()
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

    private unsafe void ApplyPlaceholder(string? placeholder)
    {
        if (string.IsNullOrEmpty(placeholder))
        {
            EditorView.SetPlaceholderStyledText([]);
            return;
        }

        byte[] textBytes = Encoding.UTF8.GetBytes(placeholder);
        float[] fg = [_placeholderColor.R, _placeholderColor.G, _placeholderColor.B, _placeholderColor.A];
        var nativeChunk = new NativeStyledChunk[1];
        GCHandle textPin = default;
        GCHandle colorPin = default;

        try
        {
            textPin = GCHandle.Alloc(textBytes, GCHandleType.Pinned);
            colorPin = GCHandle.Alloc(fg, GCHandleType.Pinned);

            nativeChunk[0] = new NativeStyledChunk
            {
                TextPtr = textPin.AddrOfPinnedObject(),
                TextLen = (nuint)textBytes.Length,
                FgPtr = colorPin.AddrOfPinnedObject(),
                Attributes = (uint)TextAttributes.None,
            };

            EditorView.SetPlaceholderStyledText(nativeChunk);
        }
        finally
        {
            if (colorPin.IsAllocated) colorPin.Free();
            if (textPin.IsAllocated) textPin.Free();
        }
    }

    #endregion
}
