using Xunit;

namespace OpenTui.Core.Tests;

public sealed class MarkdownRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public MarkdownRenderableTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
        });
    }

    public void Dispose() => _renderer.Dispose();

    private void RenderFrame()
    {
        _renderer.RenderTestFrame();
        _renderer.RenderTestFrame();
    }

    private MarkdownRenderable CreateMarkdown(
        string content,
        SyntaxStyle syntaxStyle,
        bool conceal = true,
        bool concealCode = false,
        MarkdownTableOptions? tableOptions = null) =>
        new(_renderer, new MarkdownOptions
        {
            Id = "markdown",
            Content = content,
            SyntaxStyle = syntaxStyle,
            Conceal = conceal,
            ConcealCode = concealCode,
            Fg = Rgba.FromHex("#E6EDF3"),
            Bg = Rgba.FromHex("#0D1117"),
            Width = DimensionValue.Percent(100),
            TableOptions = tableOptions,
        });

    private static SyntaxStyle CreateSyntaxStyle(Rgba? headingColor = null)
    {
        var syntaxStyle = SyntaxStyle.Create();
        syntaxStyle.Register("default", Rgba.FromHex("#E6EDF3"));
        syntaxStyle.Register("conceal", Rgba.FromHex("#6E7681"));
        syntaxStyle.Register("label", Rgba.FromHex("#7EE787"));
        syntaxStyle.Register("markup.heading", headingColor ?? Rgba.FromHex("#58A6FF"), attrs: TextAttributes.Bold);
        syntaxStyle.Register("markup.heading.1", headingColor ?? Rgba.FromHex("#58A6FF"),
            attrs: TextAttributes.Bold | TextAttributes.Underline);
        syntaxStyle.Register("markup.strong", Rgba.FromHex("#F0F6FC"), attrs: TextAttributes.Bold);
        syntaxStyle.Register("markup.italic", Rgba.FromHex("#F0F6FC"), attrs: TextAttributes.Italic);
        syntaxStyle.Register("markup.raw", Rgba.FromHex("#A5D6FF"), Rgba.FromHex("#161B22"));
        syntaxStyle.Register("markup.raw.block", Rgba.FromHex("#A5D6FF"), Rgba.FromHex("#161B22"));
        syntaxStyle.Register("markup.raw.inline", Rgba.FromHex("#A5D6FF"), Rgba.FromHex("#161B22"));
        syntaxStyle.Register("markup.link", Rgba.FromHex("#58A6FF"), attrs: TextAttributes.Underline);
        syntaxStyle.Register("markup.link.label", Rgba.FromHex("#A5D6FF"), attrs: TextAttributes.Underline);
        syntaxStyle.Register("markup.link.url", Rgba.FromHex("#58A6FF"), attrs: TextAttributes.Underline);
        syntaxStyle.Register("markup.list", Rgba.FromHex("#FF7B72"));
        syntaxStyle.Register("markup.quote", Rgba.FromHex("#8B949E"), attrs: TextAttributes.Italic);
        return syntaxStyle;
    }

    private static unsafe uint ReadCellChar(OptimizedBuffer buf, uint x, uint y)
    {
        nint charPtr = buf.GetCharPtr();
        int offset = (int)(y * buf.Width + x);
        return ((uint*)charPtr)[offset];
    }

    private static unsafe Rgba ReadCellFg(OptimizedBuffer buf, uint x, uint y)
    {
        nint fgPtr = buf.GetFgPtr();
        int floatOff = (int)(y * buf.Width + x) * 4;
        float* fp = (float*)fgPtr;
        return new Rgba(fp[floatOff], fp[floatOff + 1], fp[floatOff + 2], fp[floatOff + 3]);
    }

    private static string ReadRowText(OptimizedBuffer buf, int x, int y, int width)
    {
        var chars = new char[width];
        for (int i = 0; i < width; i++)
        {
            uint codepoint = ReadCellChar(buf, (uint)(x + i), (uint)y);
            chars[i] = codepoint is > 0 and <= char.MaxValue ? (char)codepoint : ' ';
        }

        return new string(chars).TrimEnd();
    }

    private string ReadRenderableRow(Renderable renderable, int row) =>
        ReadRowText(_renderer.NextRenderBuffer, (int)renderable.ScreenX, (int)renderable.ScreenY + row, renderable.Width);

    [Fact]
    public void Heading_ConcealTrue_HidesMarker()
    {
        using var syntaxStyle = CreateSyntaxStyle();
        var markdown = CreateMarkdown("# Hello", syntaxStyle, conceal: true);

        _renderer.Root.Add(markdown);
        RenderFrame();

        var heading = Assert.IsType<TextRenderable>(markdown.GetChildren()[0]);
        Assert.Equal("Hello", ReadRenderableRow(heading, 0));
    }

    [Fact]
    public void Heading_ConcealFalse_ShowsMarker()
    {
        using var syntaxStyle = CreateSyntaxStyle();
        var markdown = CreateMarkdown("# Hello", syntaxStyle, conceal: false);

        _renderer.Root.Add(markdown);
        RenderFrame();

        var heading = Assert.IsType<TextRenderable>(markdown.GetChildren()[0]);
        Assert.Equal("# Hello", ReadRenderableRow(heading, 0));
    }

    [Fact]
    public void LinkConcealToggle_RebuildsExistingParagraph()
    {
        using var syntaxStyle = CreateSyntaxStyle();
        var markdown = CreateMarkdown("[OpenTUI](https://github.com)", syntaxStyle, conceal: true);

        _renderer.Root.Add(markdown);
        RenderFrame();

        var paragraph = Assert.IsType<TextRenderable>(markdown.GetChildren()[0]);
        Assert.Equal("OpenTUI (https://github.com)", ReadRenderableRow(paragraph, 0));

        markdown.Conceal = false;
        RenderFrame();

        paragraph = Assert.IsType<TextRenderable>(markdown.GetChildren()[0]);
        Assert.Equal("[OpenTUI](https://github.com)", ReadRenderableRow(paragraph, 0));
    }

    [Fact]
    public void ConcealCodeToggle_RebuildsMarkdownFence()
    {
        using var syntaxStyle = CreateSyntaxStyle();
        var markdown = CreateMarkdown("""
            ```markdown
            # Hidden
            ```
            """, syntaxStyle, conceal: true, concealCode: false);

        _renderer.Root.Add(markdown);
        RenderFrame();

        var code = Assert.IsType<CodeRenderable>(markdown.GetChildren()[0]);
        Assert.Equal("# Hidden", ReadRenderableRow(code, 0));

        markdown.ConcealCode = true;
        RenderFrame();

        var concealedCode = Assert.IsType<TextRenderable>(markdown.GetChildren()[0]);
        var row = ReadRenderableRow(concealedCode, 0);
        Assert.StartsWith("Hidden", row);
        Assert.DoesNotContain("#", row);
    }

    [Fact]
    public void Table_RendersFormattedGrid()
    {
        using var syntaxStyle = CreateSyntaxStyle();
        var markdown = CreateMarkdown("""
            | A | B |
            |---|---|
            | 1 | 2 |
            """, syntaxStyle, tableOptions: new MarkdownTableOptions { ColumnWidthMode = "content" });

        _renderer.Root.Add(markdown);
        RenderFrame();

        var table = Assert.IsType<TextTableRenderable>(markdown.GetChildren()[0]);
        Assert.StartsWith("┌", ReadRenderableRow(table, 0));
        var headerRow = ReadRenderableRow(table, 1);
        var dataRow = ReadRenderableRow(table, 3);
        Assert.StartsWith("│", headerRow);
        Assert.Contains("A", headerRow);
        Assert.Contains("B", headerRow);
        Assert.StartsWith("│", dataRow);
        Assert.Contains("1", dataRow);
        Assert.Contains("2", dataRow);
    }

    [Fact]
    public void SyntaxStyleSetter_RebuildsStyledForeground()
    {
        using var redStyle = CreateSyntaxStyle(Rgba.FromHex("#FF0000"));
        using var greenStyle = CreateSyntaxStyle(Rgba.FromHex("#00FF00"));
        var markdown = CreateMarkdown("# Hello", redStyle, conceal: true);

        _renderer.Root.Add(markdown);
        RenderFrame();

        var heading = Assert.IsType<TextRenderable>(markdown.GetChildren()[0]);
        var before = ReadCellFg(_renderer.NextRenderBuffer, (uint)heading.ScreenX, (uint)heading.ScreenY);

        markdown.SyntaxStyle = greenStyle;
        RenderFrame();

        heading = Assert.IsType<TextRenderable>(markdown.GetChildren()[0]);
        var after = ReadCellFg(_renderer.NextRenderBuffer, (uint)heading.ScreenX, (uint)heading.ScreenY);

        Assert.True(before.R > 0.9f && before.G < 0.2f && before.B < 0.2f,
            $"Expected red heading before style swap, got ({before.R:F2}, {before.G:F2}, {before.B:F2}).");
        Assert.True(after.G > 0.9f && after.R < 0.2f && after.B < 0.2f,
            $"Expected green heading after style swap, got ({after.R:F2}, {after.G:F2}, {after.B:F2}).");
    }
}
