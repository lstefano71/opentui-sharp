using System.Text;

using OpenTui.Core;

using Xunit;

namespace OpenTui.Core.Tests;

public sealed class SelectRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public SelectRenderableTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 40,
            Height = 12,
        });
    }

    public void Dispose() => _renderer.Dispose();

    private void RenderFrame() => _renderer.RenderTestFrame();

    private static unsafe string ReadText(OptimizedBuffer buf, uint x, uint y, int length)
    {
        nint charPtr = buf.GetCharPtr();
        uint* chars = (uint*)charPtr;
        var sb = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            uint codePoint = chars[(int)(y * buf.Width + x + (uint)i)];
            if (codePoint == 0 || codePoint > 0x10FFFF || !Rune.TryCreate((int)codePoint, out var rune))
            {
                sb.Append(' ');
                continue;
            }

            sb.Append(rune.ToString());
        }

        return sb.ToString();
    }

    [Fact]
    public void BufferedSelectRenderable_WithBorder_RendersItemsInsideInnerContent()
    {
        var select = new SelectRenderable(_renderer, new SelectOptions
        {
            Id = "fruit-select",
            Width = DimensionValue.Point(16),
            Height = DimensionValue.Point(4),
            MarginLeft = DimensionValue.Point(2),
            MarginTop = DimensionValue.Point(1),
            Buffered = true,
            Border = true,
            ShowDescription = false,
            Options =
            [
                new() { Name = "Apple" },
                new() { Name = "Apricot" },
                new() { Name = "Avocado" },
            ],
        });

        _renderer.Root.Add(select);
        RenderFrame();

        var topBorderRow = ReadText(_renderer.NextRenderBuffer, (uint)select.ScreenX, (uint)select.ScreenY, select.Width);
        var firstContentRow = ReadText(_renderer.NextRenderBuffer, (uint)select.ScreenX, (uint)(select.ScreenY + 1), select.Width);

        Assert.DoesNotContain("Apple", topBorderRow);
        Assert.Contains("Apple", firstContentRow);
    }

    [Fact]
    public void BufferedSelectRenderable_WithBorder_ScrollsUsingInnerHeight()
    {
        var select = new SelectRenderable(_renderer, new SelectOptions
        {
            Id = "fruit-select",
            Width = DimensionValue.Point(16),
            Height = DimensionValue.Point(4),
            MarginLeft = DimensionValue.Point(2),
            MarginTop = DimensionValue.Point(1),
            Buffered = true,
            Border = true,
            ShowDescription = false,
            Options =
            [
                new() { Name = "Apple" },
                new() { Name = "Apricot" },
                new() { Name = "Avocado" },
            ],
        });

        _renderer.Root.Add(select);
        select.SelectedIndex = 2;
        RenderFrame();

        var firstContentRow = ReadText(_renderer.NextRenderBuffer, (uint)select.ScreenX, (uint)(select.ScreenY + 1), select.Width);
        var secondContentRow = ReadText(_renderer.NextRenderBuffer, (uint)select.ScreenX, (uint)(select.ScreenY + 2), select.Width);
        var bottomBorderRow = ReadText(_renderer.NextRenderBuffer, (uint)select.ScreenX, (uint)(select.ScreenY + 3), select.Width);

        Assert.Contains("Apricot", firstContentRow);
        Assert.Contains("Avocado", secondContentRow);
        Assert.DoesNotContain("Avocado", bottomBorderRow);
    }

    [Fact]
    public void Mouse_SingleClick_SelectsItem()
    {
        var select = new SelectRenderable(_renderer, new SelectOptions
        {
            Id = "click-select",
            Width = DimensionValue.Point(20),
            Height = DimensionValue.Point(5),
            Border = true,
            ShowDescription = false,
            Options =
            [
                new() { Name = "Alpha" },
                new() { Name = "Beta" },
                new() { Name = "Gamma" },
            ],
        });

        _renderer.Root.Add(select);
        RenderFrame();

        // Click on the second item (Y = screenY + 1 border + 1 item)
        int clickY = (int)select.ScreenY + 1 + 1;
        select.ProcessMouseEvent(new UiMouseEvent
        {
            Type = MouseEventType.Down,
            Button = (int)MouseButton.Left,
            X = (int)select.ScreenX + 2,
            Y = clickY,
        });

        Assert.Equal(1, select.SelectedIndex);
    }

    [Fact]
    public void Mouse_DoubleClick_EmitsItemSelected()
    {
        var select = new SelectRenderable(_renderer, new SelectOptions
        {
            Id = "dblclick-select",
            Width = DimensionValue.Point(20),
            Height = DimensionValue.Point(5),
            Border = true,
            ShowDescription = false,
            Options =
            [
                new() { Name = "Alpha" },
                new() { Name = "Beta" },
                new() { Name = "Gamma" },
            ],
        });

        _renderer.Root.Add(select);
        RenderFrame();

        (int Index, SelectOption? Option)? emitted = null;
        select.On<(int Index, SelectOption? Option)>(
            SelectRenderable.Events.ItemSelected,
            args => emitted = args);

        int clickY = (int)select.ScreenY + 1 + 1; // border + second item
        var evt = new UiMouseEvent
        {
            Type = MouseEventType.Down,
            Button = (int)MouseButton.Left,
            X = (int)select.ScreenX + 2,
            Y = clickY,
        };

        // First click — selects, no ItemSelected
        select.ProcessMouseEvent(evt);
        Assert.Null(emitted);

        // Second click — double-click triggers ItemSelected
        select.ProcessMouseEvent(evt);
        Assert.NotNull(emitted);
        Assert.Equal(1, emitted.Value.Index);
        Assert.Equal("Beta", emitted.Value.Option?.Name);
    }
}
