using OpenTui;
using Xunit;

namespace OpenTui.Tests.Widgets;

public class WidgetTests
{
    [Fact]
    public void Add_SetsParent()
    {
        var parent = new Box();
        var child = new Text("Hello");

        parent.Add(child);

        Assert.Same(parent, child.Parent);
        Assert.Single(parent.Children);
    }

    [Fact]
    public void Add_DuplicateParent_Throws()
    {
        var parent1 = new Box();
        var parent2 = new Box();
        var child = new Text("Hello");

        parent1.Add(child);
        Assert.Throws<InvalidOperationException>(() => parent2.Add(child));
    }

    [Fact]
    public void Remove_ClearsParent()
    {
        var parent = new Box();
        var child = new Text("Hello");

        parent.Add(child);
        parent.Remove(child);

        Assert.Null(child.Parent);
        Assert.Empty(parent.Children);
    }

    [Fact]
    public void Clear_RemovesAllChildren()
    {
        var parent = new Box();
        parent.Add(new Text("A"));
        parent.Add(new Text("B"));

        parent.Clear();

        Assert.Empty(parent.Children);
    }

    [Fact]
    public void ResolvedFg_InheritsFromParent()
    {
        var parent = new Box { Fg = Rgba.FromHex("#FF0000") };
        var child = new Text("Hello");
        parent.Add(child);

        Assert.Equal(parent.Fg, child.ResolvedFg);
    }

    [Fact]
    public void ResolvedFg_OwnValue_OverridesParent()
    {
        var parent = new Box { Fg = Rgba.FromHex("#FF0000") };
        var child = new Text("Hello") { Fg = Rgba.FromHex("#00FF00") };
        parent.Add(child);

        Assert.Equal(Rgba.FromHex("#00FF00"), child.ResolvedFg);
    }

    [Fact]
    public void CollectionInitializer_Works()
    {
        var box = new Box
        {
            new Text("A"),
            new Text("B"),
            new Text("C"),
        };

        Assert.Equal(3, box.Children.Count);
    }
}
