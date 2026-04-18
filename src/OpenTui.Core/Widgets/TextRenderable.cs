using Facebook.Yoga;

namespace OpenTui.Core;

/// <summary>
/// Styled text display renderable.
/// Manages content (string or StyledText), delegates rendering to native TextBufferView.
/// Matches TypeScript TextRenderable from Text.ts.
/// </summary>
public class TextRenderable : TextBufferRenderable
{
    private StyledText? _content;

    public TextRenderable(IRenderContext ctx, TextOptions? options = null)
        : base(ctx, options ?? new TextOptions())
    {
        options ??= new TextOptions();

        if (options.StyledContent is { } styled)
        {
            SetContent(styled);
        }
        else if (options.Content is { } text)
        {
            SetContent(text);
        }
    }

    #region Content

    /// <summary>Gets or sets the text content as a string.</summary>
    public string? ContentText
    {
        get => _content?.PlainText;
        set
        {
            if (value is null)
            {
                _content = null;
                TextBuffer.Clear();
            }
            else
            {
                SetContent(value);
            }
        }
    }

    /// <summary>Gets or sets the styled text content.</summary>
    public StyledText? Content
    {
        get => _content;
        set
        {
            if (value is null)
            {
                _content = null;
                TextBuffer.Clear();
            }
            else
            {
                SetContent(value);
            }
        }
    }

    /// <summary>Sets plain string content.</summary>
    public void SetContent(string text)
    {
        _content = new StyledText(TextChunk.Plain(text));
        SetStyledTextAndDirtyLayout(_content);
        RequestRender();
    }

    /// <summary>Sets styled text content.</summary>
    public void SetContent(StyledText styledText)
    {
        _content = styledText;
        SetStyledTextAndDirtyLayout(styledText);
        RequestRender();
    }

    /// <summary>Clears all text content.</summary>
    public void Clear()
    {
        _content = null;
        TextBuffer.Clear();
        RequestRender();
    }

    #endregion
}
