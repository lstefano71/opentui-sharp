using Facebook.Yoga;
using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Tests for the Yoga layout integration.
/// Verifies that LayoutConfig, YogaNodeExtensions, and LayoutOptions
/// correctly configure Yoga nodes and produce expected layout results.
/// </summary>
public class LayoutTests
{
    [Fact]
    public void SharedConfig_IsNotNull()
    {
        var config = LayoutConfig.Shared;
        Assert.NotNull(config);
    }

    [Fact]
    public void CreateLayoutNode_ReturnsValidNode()
    {
        var node = YogaNodeExtensions.CreateLayoutNode();
        Assert.NotNull(node);
        YGNodeAPI.YGNodeFree(node);
    }

    [Fact]
    public void SimpleColumnLayout_ComputesCorrectly()
    {
        var root = YogaNodeExtensions.CreateLayoutNode();
        YGNodeStyleAPI.YGNodeStyleSetWidth(root, 100);
        YGNodeStyleAPI.YGNodeStyleSetHeight(root, 100);
        YGNodeStyleAPI.YGNodeStyleSetFlexDirection(root, YGFlexDirection.Column);

        var child1 = YogaNodeExtensions.CreateLayoutNode();
        YGNodeStyleAPI.YGNodeStyleSetHeight(child1, 30);

        var child2 = YogaNodeExtensions.CreateLayoutNode();
        YGNodeStyleAPI.YGNodeStyleSetFlexGrow(child2, 1);

        YGNodeAPI.YGNodeInsertChild(root, child1, 0);
        YGNodeAPI.YGNodeInsertChild(root, child2, 1);

        YGNodeAPI.YGNodeCalculateLayout(root, 100, 100, YGDirection.LTR);

        var c1 = child1.GetComputedLayout();
        Assert.Equal(0f, c1.Top);
        Assert.Equal(0f, c1.Left);
        Assert.Equal(100f, c1.Width);
        Assert.Equal(30f, c1.Height);

        var c2 = child2.GetComputedLayout();
        Assert.Equal(30f, c2.Top);
        Assert.Equal(0f, c2.Left);
        Assert.Equal(100f, c2.Width);
        Assert.Equal(70f, c2.Height);

        YGNodeAPI.YGNodeFree(root);
    }

    [Fact]
    public void ApplyLayoutOptions_SetsFlexProperties()
    {
        var node = YogaNodeExtensions.CreateLayoutNode();
        node.ApplyLayoutOptions(new LayoutOptions
        {
            Width = 200,
            Height = 100,
            FlexDirection = FlexDirectionValue.Row,
            JustifyContent = JustifyValue.Center,
            AlignItems = AlignValue.Center,
            Padding = 5,
        });

        var child = YogaNodeExtensions.CreateLayoutNode();
        child.ApplyLayoutOptions(new LayoutOptions
        {
            Width = 50,
            Height = 50,
        });

        YGNodeAPI.YGNodeInsertChild(node, child, 0);
        YGNodeAPI.YGNodeCalculateLayout(node, float.NaN, float.NaN, YGDirection.LTR);

        var layout = child.GetComputedLayout();
        // Centered in 200x100 with 5px padding: available 190x90, child 50x50
        Assert.Equal(75f, layout.Left);   // (190-50)/2 + 5
        Assert.Equal(25f, layout.Top);    // (90-50)/2 + 5
        Assert.Equal(50f, layout.Width);
        Assert.Equal(50f, layout.Height);

        YGNodeAPI.YGNodeFree(node);
    }

    [Fact]
    public void RowLayout_WithFlexGrow_DistributesSpace()
    {
        var root = YogaNodeExtensions.CreateLayoutNode();
        root.ApplyLayoutOptions(new LayoutOptions
        {
            Width = 300,
            Height = 50,
            FlexDirection = FlexDirectionValue.Row,
        });

        var fixed1 = YogaNodeExtensions.CreateLayoutNode();
        fixed1.ApplyLayoutOptions(new LayoutOptions { Width = 100 });

        var flexible = YogaNodeExtensions.CreateLayoutNode();
        YGNodeStyleAPI.YGNodeStyleSetFlexGrow(flexible, 1);

        var fixed2 = YogaNodeExtensions.CreateLayoutNode();
        fixed2.ApplyLayoutOptions(new LayoutOptions { Width = 50 });

        YGNodeAPI.YGNodeInsertChild(root, fixed1, 0);
        YGNodeAPI.YGNodeInsertChild(root, flexible, 1);
        YGNodeAPI.YGNodeInsertChild(root, fixed2, 2);

        YGNodeAPI.YGNodeCalculateLayout(root, float.NaN, float.NaN, YGDirection.LTR);

        Assert.Equal(100f, fixed1.GetComputedLayout().Width);
        Assert.Equal(150f, flexible.GetComputedLayout().Width); // 300 - 100 - 50
        Assert.Equal(50f, fixed2.GetComputedLayout().Width);

        YGNodeAPI.YGNodeFree(root);
    }

    [Fact]
    public void PercentDimensions_WorkCorrectly()
    {
        var root = YogaNodeExtensions.CreateLayoutNode();
        root.ApplyLayoutOptions(new LayoutOptions
        {
            Width = 200,
            Height = 200,
        });

        var child = YogaNodeExtensions.CreateLayoutNode();
        child.ApplyLayoutOptions(new LayoutOptions
        {
            Width = DimensionValue.Percent(50),
            Height = DimensionValue.Percent(25),
        });

        YGNodeAPI.YGNodeInsertChild(root, child, 0);
        YGNodeAPI.YGNodeCalculateLayout(root, float.NaN, float.NaN, YGDirection.LTR);

        var layout = child.GetComputedLayout();
        Assert.Equal(100f, layout.Width);  // 50% of 200
        Assert.Equal(50f, layout.Height);  // 25% of 200

        YGNodeAPI.YGNodeFree(root);
    }

    [Fact]
    public void Margin_AppliesCorrectly()
    {
        var root = YogaNodeExtensions.CreateLayoutNode();
        root.ApplyLayoutOptions(new LayoutOptions
        {
            Width = 100,
            Height = 100,
        });

        var child = YogaNodeExtensions.CreateLayoutNode();
        child.ApplyLayoutOptions(new LayoutOptions
        {
            Width = 50,
            Height = 50,
            MarginTop = 10,
            MarginLeft = 20,
        });

        YGNodeAPI.YGNodeInsertChild(root, child, 0);
        YGNodeAPI.YGNodeCalculateLayout(root, float.NaN, float.NaN, YGDirection.LTR);

        var layout = child.GetComputedLayout();
        Assert.Equal(20f, layout.Left);
        Assert.Equal(10f, layout.Top);

        YGNodeAPI.YGNodeFree(root);
    }

    [Fact]
    public void Gap_CreateSpaceBetweenChildren()
    {
        var root = YogaNodeExtensions.CreateLayoutNode();
        root.ApplyLayoutOptions(new LayoutOptions
        {
            Width = 100,
            Height = 100,
            FlexDirection = FlexDirectionValue.Column,
            Gap = 10,
        });

        var c1 = YogaNodeExtensions.CreateLayoutNode();
        YGNodeStyleAPI.YGNodeStyleSetHeight(c1, 20);
        var c2 = YogaNodeExtensions.CreateLayoutNode();
        YGNodeStyleAPI.YGNodeStyleSetHeight(c2, 20);

        YGNodeAPI.YGNodeInsertChild(root, c1, 0);
        YGNodeAPI.YGNodeInsertChild(root, c2, 1);

        YGNodeAPI.YGNodeCalculateLayout(root, float.NaN, float.NaN, YGDirection.LTR);

        Assert.Equal(0f, c1.GetComputedLayout().Top);
        Assert.Equal(30f, c2.GetComputedLayout().Top); // 20 + 10 gap

        YGNodeAPI.YGNodeFree(root);
    }

    [Fact]
    public void EnumMappers_ProduceCorrectValues()
    {
        Assert.Equal(YGFlexDirection.Row, FlexDirectionValue.Row.ToYoga());
        Assert.Equal(YGFlexDirection.Column, FlexDirectionValue.Column.ToYoga());
        Assert.Equal(YGWrap.Wrap, WrapValue.Wrap.ToYoga());
        Assert.Equal(YGAlign.Center, AlignValue.Center.ToYogaAlign());
        Assert.Equal(YGJustify.SpaceBetween, JustifyValue.SpaceBetween.ToYoga());
        Assert.Equal(YGPositionType.Absolute, PositionValue.Absolute.ToYoga());
        Assert.Equal(YGOverflow.Hidden, OverflowValue.Hidden.ToYoga());
    }
}
