using OpenTui;
using Xunit;

namespace OpenTui.Tests.Widgets;

public class FocusManagerTests
{
    [Fact]
    public void Focused_Initially_IsNull()
    {
        var fm = new FocusManager();
        Assert.Null(fm.Focused);
    }

    [Fact]
    public void FocusNext_CyclesToFirst()
    {
        var fm = new FocusManager();
        var w1 = new Text("A");
        var w2 = new Text("B");

        fm.Register(w1);
        fm.Register(w2);
        fm.FocusNext();

        Assert.Same(w1, fm.Focused);
    }

    [Fact]
    public void FocusNext_Wraps()
    {
        var fm = new FocusManager();
        var w1 = new Text("A");
        var w2 = new Text("B");

        fm.Register(w1);
        fm.Register(w2);
        fm.FocusNext(); // w1
        fm.FocusNext(); // w2
        fm.FocusNext(); // wraps to w1

        Assert.Same(w1, fm.Focused);
    }

    [Fact]
    public void FocusPrevious_Wraps()
    {
        var fm = new FocusManager();
        var w1 = new Text("A");
        var w2 = new Text("B");

        fm.Register(w1);
        fm.Register(w2);
        fm.FocusNext(); // w1
        fm.FocusPrevious(); // wraps to w2

        Assert.Same(w2, fm.Focused);
    }

    [Fact]
    public void Focus_SpecificWidget()
    {
        var fm = new FocusManager();
        var w1 = new Text("A");
        var w2 = new Text("B");

        fm.Register(w1);
        fm.Register(w2);
        fm.Focus(w2);

        Assert.Same(w2, fm.Focused);
    }

    [Fact]
    public void Unregister_AdjustsIndex()
    {
        var fm = new FocusManager();
        var w1 = new Text("A");
        var w2 = new Text("B");

        fm.Register(w1);
        fm.Register(w2);
        fm.Focus(w2);
        fm.Unregister(w2);

        Assert.Same(w1, fm.Focused);
    }

    [Fact]
    public void ClearFocus_SetsNull()
    {
        var fm = new FocusManager();
        var w1 = new Text("A");

        fm.Register(w1);
        fm.FocusNext();
        fm.ClearFocus();

        Assert.Null(fm.Focused);
    }
}
