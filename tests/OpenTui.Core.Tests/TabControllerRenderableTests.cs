using System.Text;

using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public class TabControllerRenderableTests
{
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
    public void AddTab_InitializesAndShowsFirstTab()
    {
        var ctx = new TestRenderContext();
        var controller = new TabControllerRenderable(ctx, new TabControllerOptions
        {
            Id = "controller",
            Width = DimensionValue.Point(80),
            Height = DimensionValue.Point(24),
        });

        int initCount = 0;
        int showCount = 0;

        controller.AddTab(new TabControllerTab
        {
            Title = "First",
            Initialize = _ => initCount++,
            Show = () => showCount++,
        });

        Assert.Equal(1, initCount);
        Assert.Equal(1, showCount);
        Assert.Equal(0, controller.GetCurrentTabIndex());
        Assert.Equal("First", controller.GetCurrentTab()!.Title);

        controller.DestroyRecursively();
    }

    [Fact]
    public void SwitchToTab_HidesPreviousAndInitializesNext()
    {
        var ctx = new TestRenderContext();
        var controller = new TabControllerRenderable(ctx);

        int firstHide = 0;
        int secondInit = 0;
        int secondShow = 0;

        controller.AddTab(new TabControllerTab
        {
            Title = "First",
            Initialize = _ => { },
            Hide = () => firstHide++,
        });

        controller.AddTab(new TabControllerTab
        {
            Title = "Second",
            Initialize = _ => secondInit++,
            Show = () => secondShow++,
        });

        controller.SwitchToTab(1);

        Assert.Equal(1, firstHide);
        Assert.Equal(1, secondInit);
        Assert.Equal(1, secondShow);
        Assert.Equal(1, controller.GetCurrentTabIndex());
        Assert.Equal("Second", controller.GetCurrentTab()!.Title);

        controller.DestroyRecursively();
    }

    [Fact]
    public void TabStripSelection_ChangesCurrentTab()
    {
        var ctx = new TestRenderContext();
        var controller = new TabControllerRenderable(ctx);

        controller.AddTab(new TabControllerTab
        {
            Title = "One",
            Initialize = _ => { },
        });

        controller.AddTab(new TabControllerTab
        {
            Title = "Two",
            Initialize = _ => { },
        });

        controller.TabStrip.SelectedIndex = 1;

        Assert.Equal(1, controller.GetCurrentTabIndex());
        Assert.Equal("Two", controller.GetCurrentTab()!.Title);

        controller.DestroyRecursively();
    }

    [Fact]
    public void UpdateLayout_CallsUpdateOnCurrentTabOnly()
    {
        var ctx = new TestRenderContext();
        var controller = new TabControllerRenderable(ctx, new TabControllerOptions
        {
            Width = DimensionValue.Point(80),
            Height = DimensionValue.Point(24),
        });

        int firstUpdates = 0;
        int secondUpdates = 0;

        controller.AddTab(new TabControllerTab
        {
            Title = "One",
            Initialize = _ => { },
            Update = (_, _) => firstUpdates++,
        });

        controller.AddTab(new TabControllerTab
        {
            Title = "Two",
            Initialize = _ => { },
            Update = (_, _) => secondUpdates++,
        });

        controller.UpdateLayout(16f, []);
        controller.SwitchToTab(1);
        controller.UpdateLayout(16f, []);

        Assert.Equal(1, firstUpdates);
        Assert.Equal(1, secondUpdates);

        controller.DestroyRecursively();
    }

    [Fact]
    public void Focus_DelegatesToTabStrip()
    {
        var ctx = new TestRenderContext();
        var controller = new TabControllerRenderable(ctx);

        controller.AddTab(new TabControllerTab
        {
            Title = "Only",
            Initialize = _ => { },
        });

        controller.Focus();
        Assert.True(controller.TabStrip.Focused);

        controller.Blur();
        Assert.False(controller.TabStrip.Focused);

        controller.DestroyRecursively();
    }

    [Fact]
    public void CustomDescription_CoexistsWithDefaultHelpText()
    {
        var ctx = new TestRenderContext();
        var controller = new TabControllerRenderable(ctx);

        controller.AddTab(new TabControllerTab
        {
            Title = "One",
            Description = "Custom description",
            Initialize = _ => { },
        });

        controller.AddTab(new TabControllerTab
        {
            Title = "Two",
            Initialize = _ => { },
        });

        Assert.Equal("Custom description", controller.TabStrip.GetSelectedOption()!.Description);
        Assert.Equal(
            "Tab 1/2 - Use Left/Right arrows to navigate | Press Ctrl+C to exit | D or .: toggle debug | Ctrl+G: dump hit grid",
            controller.GetCurrentHelpText());

        controller.SwitchToTab(1);

        Assert.Equal(
            "Tab 2/2 - Use Left/Right arrows to navigate | Press Ctrl+C to exit | D or .: toggle debug | Ctrl+G: dump hit grid",
            controller.GetCurrentHelpText());

        controller.DestroyRecursively();
    }

    [Fact]
    public void TransparentControllerBackground_StillRendersHelpLine()
    {
        using var renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 100,
            Height = 24,
        });

        var controller = new TabControllerRenderable(renderer, new TabControllerOptions
        {
            Width = DimensionValue.Point(100),
            Height = DimensionValue.Point(24),
            TabBarHeight = 4,
        });

        controller.AddTab(new TabControllerTab
        {
            Title = "One",
            Description = "Custom description",
            Initialize = _ => { },
        });

        renderer.Root.Add(controller);
        renderer.RenderTestFrame();

        string helpRow = ReadText(renderer.NextRenderBuffer, 1, 3, 80);
        Assert.Contains("Use Left/Right arrows to navigate", helpRow);
    }
}
