using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public sealed class CompositionRenderableTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public CompositionRenderableTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
        });
    }

    public void Dispose() => _renderer.Dispose();

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
    public void GenericRenderable_UsesCustomRenderCallback()
    {
        var generic = new GenericRenderable(_renderer, new GenericOptions
        {
            Id = "generic",
            Width = DimensionValue.Point(12),
            Height = DimensionValue.Point(3),
            Render = (buffer, _, renderable) =>
            {
                buffer.DrawText("GENERIC", (uint)renderable.X, (uint)renderable.Y, Rgba.White);
            }
        });

        _renderer.Root.Add(generic);
        _renderer.RenderTestFrame();

        string row = ReadRowText(_renderer.NextRenderBuffer, (int)generic.ScreenX, (int)generic.ScreenY, generic.Width);
        Assert.Contains("GENERIC", row);
    }

    [Fact]
    public void DelegatingRenderable_AddAndFocusRouteToConfiguredTargets()
    {
        var input = new InputRenderable(_renderer, new InputOptions
        {
            Id = "delegate-input",
            Width = DimensionValue.Point(10),
        });
        var content = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "delegate-content",
            FlexDirection = FlexDirectionValue.Row,
        });
        var root = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "delegate-root",
            FlexDirection = FlexDirectionValue.Column,
        });
        root.Add(input);
        root.Add(content);

        var delegated = new DelegatingRenderable(_renderer, new DelegatingOptions
        {
            Id = "delegate-wrapper",
            Root = root,
            AddTargetId = "delegate-content",
            RemoveTargetId = "delegate-content",
            FocusTargetId = "delegate-input",
        });

        _renderer.Root.Add(delegated);

        var child = new TextRenderable(_renderer, new TextOptions
        {
            Id = "delegated-child",
            Content = "child",
        });

        delegated.Add(child);
        delegated.Focus();

        Assert.Same(child, content.GetRenderable("delegated-child"));
        Assert.True(input.Focused);
    }
}
