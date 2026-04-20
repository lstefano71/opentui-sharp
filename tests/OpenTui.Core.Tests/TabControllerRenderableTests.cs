using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public class TabControllerRenderableTests
{
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
}
