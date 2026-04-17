using OpenTui;
using Xunit;

namespace OpenTui.Tests.Layout;

public class LayoutNodeTests
{
    [Fact]
    public void CalculateLayout_FixedSize()
    {
        var node = new LayoutNode { Width = 100, Height = 50 };
        node.CalculateLayout();

        Assert.Equal(100, node.LayoutWidth);
        Assert.Equal(50, node.LayoutHeight);
    }

    [Fact]
    public void CalculateLayout_FlexGrow_DistributesSpace()
    {
        var root = new LayoutNode { Width = 100, Height = 100, FlexDirection = FlexDirection.Row };
        var child1 = new LayoutNode { FlexGrow = 1 };
        var child2 = new LayoutNode { FlexGrow = 1 };

        root.InsertChild(child1, 0);
        root.InsertChild(child2, 1);
        root.CalculateLayout();

        Assert.Equal(50, child1.LayoutWidth);
        Assert.Equal(50, child2.LayoutWidth);
    }

    [Fact]
    public void FlexDirection_Column_StacksVertically()
    {
        var root = new LayoutNode { Width = 100, Height = 100, FlexDirection = FlexDirection.Column };
        var child1 = new LayoutNode { Height = 30 };
        var child2 = new LayoutNode { Height = 40 };

        root.InsertChild(child1, 0);
        root.InsertChild(child2, 1);
        root.CalculateLayout();

        Assert.Equal(0, child1.LayoutY);
        Assert.Equal(30, child2.LayoutY);
    }

    [Fact]
    public void LayoutBounds_ReturnsCorrectRect()
    {
        var root = new LayoutNode { Width = 80, Height = 24 };
        root.CalculateLayout();

        var bounds = root.LayoutBounds;
        Assert.Equal(0, bounds.X);
        Assert.Equal(0, bounds.Y);
        Assert.Equal(80, bounds.Width);
        Assert.Equal(24, bounds.Height);
    }

    [Fact]
    public void Visible_False_SetsDisplayNone()
    {
        var node = new LayoutNode { Visible = false };
        Assert.False(node.Visible);
    }

    [Fact]
    public void ChildManagement()
    {
        var root = new LayoutNode();
        var child = new LayoutNode();

        root.InsertChild(child, 0);
        Assert.Equal(1, root.ChildCount);

        root.RemoveChild(child);
        Assert.Equal(0, root.ChildCount);
    }
}
