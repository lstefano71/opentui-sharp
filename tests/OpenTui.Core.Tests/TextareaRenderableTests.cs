using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public sealed class TextareaRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public TextareaRenderableTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
        });
    }

    public void Dispose() => _renderer.Dispose();

    private void RenderFrame() => _renderer.RenderTestFrame();

    private static uint ReadCellChar(OptimizedBuffer buf, uint x, uint y) =>
        buf.GetCharAt(x, y);

    private static string ReadRowText(OptimizedBuffer buf, int x, int y, int width)
    {
        var chars = new char[width];
        for (int i = 0; i < width; i++)
        {
            uint codepoint = ReadCellChar(buf, (uint)(x + i), (uint)y);
            chars[i] = codepoint is > 0 and <= char.MaxValue ? (char)codepoint : ' ';
        }

        return new string(chars);
    }

    [Fact]
    public void TextareaRenderable_ShiftRight_CreatesSelectionAndSelectedText()
    {
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-selection",
            Width = DimensionValue.Point(12),
            Height = DimensionValue.Point(4),
            InitialValue = "abc",
            SelectionBg = Rgba.FromHex("#264F78"),
            SelectionFg = Rgba.FromHex("#FFFFFF"),
        });

        _renderer.Root.Add(textarea);
        textarea.Focus();
        textarea.GotoBufferHome();

        _renderer.InternalKeyInput.ProcessParsedKey(new ParsedKey
        {
            Name = "right",
            Sequence = "\u001b[C",
            Shift = true,
        });

        Assert.True(textarea.HasSelection());
        Assert.Equal("a", textarea.GetSelectedText());
    }

    [Fact]
    public void TextareaRenderable_DeleteToLineStart_AtColumnZero_JoinsPreviousLine()
    {
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-delete-line-start",
            Width = DimensionValue.Point(12),
            Height = DimensionValue.Point(4),
            InitialValue = "ab\ncd",
        });

        _renderer.Root.Add(textarea);
        textarea.EditBuffer.SetCursor(1, 0);

        textarea.DeleteToLineStart();

        Assert.Equal("abcd", textarea.GetText());
    }

    [Fact]
    public void TextareaRenderable_ProvidesLogicalLineInfo_ForLineNumberRenderable()
    {
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-lines",
            Width = DimensionValue.Point(10),
            Height = DimensionValue.Point(4),
            InitialValue = "one two three four",
            WrapMode = 2,
        });

        var lineNumbers = new LineNumberRenderable(_renderer, new LineNumberOptions
        {
            Id = "textarea-gutter",
            Target = textarea,
            Width = DimensionValue.Percent(100),
            Height = DimensionValue.Percent(100),
        });

        _renderer.Root.Add(lineNumbers);
        RenderFrame();

        var lineInfo = textarea.GetCachedLineInfo();
        Assert.NotNull(lineInfo);
        Assert.NotEmpty(lineInfo!.LineSources);
    }

    [Fact]
    public void TextareaRenderable_WithPlaceholder_RendersWithoutCrash()
    {
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-placeholder",
            Width = DimensionValue.Point(10),
            Height = DimensionValue.Point(4),
            Placeholder = "Enter text here...",
            PlaceholderColor = Rgba.FromHex("#333333"),
        });

        _renderer.Root.Add(textarea);

        RenderFrame();

        Assert.Equal(string.Empty, textarea.GetText());
    }

    [Fact]
    public void TextareaRenderable_Keypress_FiresCursorAndContentCallbacks()
    {
        int cursorChanges = 0;
        int contentChanges = 0;

        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-callbacks-keypress",
            Width = DimensionValue.Point(12),
            Height = DimensionValue.Point(4),
            OnCursorChange = _ => cursorChanges++,
            OnContentChange = () => contentChanges++,
        });

        _renderer.Root.Add(textarea);
        textarea.Focus();

        int initialCursorChanges = cursorChanges;
        int initialContentChanges = contentChanges;

        _renderer.InternalKeyInput.ProcessParsedKey(new ParsedKey
        {
            Name = "h",
            Sequence = "h",
        });

        Assert.Equal("h", textarea.GetText());
        Assert.True(cursorChanges > initialCursorChanges);
        Assert.True(contentChanges > initialContentChanges);
    }

    [Fact]
    public void TextareaRenderable_CursorMovement_FiresCursorButNotContentCallback()
    {
        int cursorChanges = 0;
        int contentChanges = 0;

        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-callbacks-move",
            Width = DimensionValue.Point(12),
            Height = DimensionValue.Point(4),
            InitialValue = "hello",
            OnCursorChange = _ => cursorChanges++,
            OnContentChange = () => contentChanges++,
        });

        _renderer.Root.Add(textarea);
        textarea.Focus();
        textarea.GotoBufferEnd();

        int initialCursorChanges = cursorChanges;
        int initialContentChanges = contentChanges;

        textarea.MoveCursorLeft();

        Assert.Equal("hello", textarea.GetText());
        Assert.True(cursorChanges > initialCursorChanges);
        Assert.Equal(initialContentChanges, contentChanges);
    }

    [Fact]
    public void TextareaRenderable_SetText_FiresContentChangeCallback()
    {
        int contentChanges = 0;

        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-callbacks-settext",
            Width = DimensionValue.Point(12),
            Height = DimensionValue.Point(4),
            InitialValue = "initial",
            OnContentChange = () => contentChanges++,
        });

        _renderer.Root.Add(textarea);

        int initialContentChanges = contentChanges;

        textarea.SetText("updated");

        Assert.Equal("updated", textarea.GetText());
        Assert.True(contentChanges > initialContentChanges);
    }

    [Fact]
    public void LineNumberRenderable_ShowLineNumbersFalse_CollapsesGutterWidth()
    {
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-toggle-lines",
            Width = DimensionValue.Point(10),
            Height = DimensionValue.Point(4),
            InitialValue = "one\ntwo\nthree",
        });

        var lineNumbers = new LineNumberRenderable(_renderer, new LineNumberOptions
        {
            Id = "textarea-toggle-gutter",
            Target = textarea,
            Width = DimensionValue.Percent(100),
            Height = DimensionValue.Percent(100),
        });

        _renderer.Root.Add(lineNumbers);
        RenderFrame();

        var gutter = Assert.Single(lineNumbers.GetChildren(), child => !ReferenceEquals(child, textarea));
        Assert.True(gutter.Width > 0);
        Assert.Contains('1', ReadRowText(_renderer.NextRenderBuffer, (int)gutter.ScreenX, (int)gutter.ScreenY, gutter.Width));

        lineNumbers.ShowLineNumbers = false;
        RenderFrame();

        Assert.DoesNotContain('1', ReadRowText(_renderer.NextRenderBuffer, (int)gutter.ScreenX, (int)gutter.ScreenY, gutter.Width));
    }

    [Fact]
    public void TextareaRenderable_Extmarks_MoveCursorSkipsVirtualRange()
    {
        using var syntaxStyle = SyntaxStyle.Create();
        uint styleId = syntaxStyle.Register("virtual", fg: Rgba.FromHex("#4ECDC4"));
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-extmarks-move",
            Width = DimensionValue.Point(20),
            Height = DimensionValue.Point(4),
            InitialValue = "a[VIRTUAL]b",
            SyntaxStyle = syntaxStyle,
        });

        _renderer.Root.Add(textarea);
        int extmarkId = textarea.Extmarks.Create(new ExtmarkOptions
        {
            Start = 1,
            End = 10,
            Virtual = true,
            StyleId = styleId,
        });
        textarea.Focus();
        textarea.GotoBufferHome();

        textarea.MoveCursorRight();
        Assert.Equal<uint>(10, textarea.CursorOffset);

        textarea.MoveCursorLeft();
        Assert.Equal<uint>(0, textarea.CursorOffset);

        var extmark = textarea.Extmarks.Get(extmarkId);
        Assert.NotNull(extmark);
        Assert.True(extmark!.Virtual);
    }

    [Fact]
    public void TextareaRenderable_Extmarks_BackspaceDeletesWholeVirtualRange()
    {
        using var syntaxStyle = SyntaxStyle.Create();
        uint styleId = syntaxStyle.Register("virtual", fg: Rgba.FromHex("#4ECDC4"));
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-extmarks-backspace",
            Width = DimensionValue.Point(20),
            Height = DimensionValue.Point(4),
            InitialValue = "a[VIRTUAL]b",
            SyntaxStyle = syntaxStyle,
        });

        _renderer.Root.Add(textarea);
        textarea.Extmarks.Create(new ExtmarkOptions
        {
            Start = 1,
            End = 10,
            Virtual = true,
            StyleId = styleId,
        });

        textarea.GotoBufferHome();
        textarea.MoveCursorRight();
        textarea.DeleteCharBackward();

        Assert.Equal("ab", textarea.GetText());
        Assert.Empty(textarea.Extmarks.GetVirtual());
    }

    [Fact]
    public void TextareaRenderable_Extmarks_AdjustAfterInsertion()
    {
        using var syntaxStyle = SyntaxStyle.Create();
        uint styleId = syntaxStyle.Register("virtual", fg: Rgba.FromHex("#4ECDC4"));
        var textarea = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "textarea-extmarks-insert",
            Width = DimensionValue.Point(20),
            Height = DimensionValue.Point(4),
            InitialValue = "a[VIRTUAL]b",
            SyntaxStyle = syntaxStyle,
        });

        _renderer.Root.Add(textarea);
        int extmarkId = textarea.Extmarks.Create(new ExtmarkOptions
        {
            Start = 1,
            End = 10,
            Virtual = true,
            StyleId = styleId,
        });

        textarea.GotoBufferHome();
        textarea.InsertText("x");

        var extmark = textarea.Extmarks.Get(extmarkId);
        Assert.NotNull(extmark);
        Assert.Equal<uint>(2, extmark!.Start);
        Assert.Equal<uint>(11, extmark.End);
    }
}
