using OpenTui.Core;
using Xunit;

namespace OpenTui.Core.Tests;

public sealed class TextWrapDemoLogicTests
{
    [Fact]
    public void GlobalHotkeyStopsPropagationBeforeFocusedInputSeesKey()
    {
        using var renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 40,
            Height = 20,
        });

        var input = new InputRenderable(renderer, new InputOptions
        {
            Id = "file-path-input",
            Width = DimensionValue.Point(20),
            Value = "",
        });
        renderer.Root.Add(input);

        bool opened = false;
        renderer.KeyInput.On<KeyEvent>("keypress", keyEvent =>
        {
            if (opened || !string.Equals(keyEvent.Name, "l", StringComparison.OrdinalIgnoreCase))
                return;

            opened = true;
            input.Focus();
            keyEvent.StopPropagation();
        });

        renderer.DispatchTestKeyInput(new ParsedKey
        {
            Name = "l",
            Sequence = "l",
        });

        Assert.True(opened);
        Assert.Equal(string.Empty, input.Value);
    }

    [Fact]
    public void ComputeResizedBounds_SouthEastResize_StaysInsideContainerWithoutShiftingTopLeft()
    {
        var bounds = TextWrapDemoLogic.ComputeResizedBounds(
            resizeDirection: "se",
            deltaX: 200,
            deltaY: 200,
            startLeft: TextWrapDemoLogic.InitialTextBoxInset,
            startTop: TextWrapDemoLogic.InitialTextBoxInset,
            startWidth: 80,
            startHeight: 15,
            minWidth: 10,
            minHeight: 5,
            containerWidth: 100,
            containerHeight: 40);

        Assert.Equal(TextWrapDemoLogic.InitialTextBoxInset, bounds.Left);
        Assert.Equal(TextWrapDemoLogic.InitialTextBoxInset, bounds.Top);
        Assert.Equal(97, bounds.Width);
        Assert.Equal(37, bounds.Height);
        Assert.Equal(98, bounds.Left + bounds.Width);
        Assert.Equal(38, bounds.Top + bounds.Height);
    }

    [Fact]
    public void ComputeResizedBounds_NorthWestResize_CanReachInnerBorderWithoutCrossingIt()
    {
        var bounds = TextWrapDemoLogic.ComputeResizedBounds(
            resizeDirection: "nw",
            deltaX: -200,
            deltaY: -200,
            startLeft: TextWrapDemoLogic.InitialTextBoxInset,
            startTop: TextWrapDemoLogic.InitialTextBoxInset,
            startWidth: 80,
            startHeight: 15,
            minWidth: 10,
            minHeight: 5,
            containerWidth: 100,
            containerHeight: 40);

        Assert.Equal(0, bounds.Left);
        Assert.Equal(0, bounds.Top);
        Assert.Equal(81, bounds.Width);
        Assert.Equal(16, bounds.Height);
    }

    [Fact]
    public void AbsoluteChildInsideBorderedPaddedParent_ComputesExpectedScreenPosition()
    {
        using var renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 40,
            Height = 20,
        });

        var parent = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "parent",
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(0),
            Top = DimensionValue.Point(0),
            Width = DimensionValue.Point(20),
            Height = DimensionValue.Point(10),
            Border = true,
            Padding = DimensionValue.Point(1),
        });

        var child = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "child",
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(1),
            Top = DimensionValue.Point(1),
            Width = DimensionValue.Point(5),
            Height = DimensionValue.Point(3),
            Border = true,
        });

        renderer.Root.Add(parent);
        parent.Add(child);
        renderer.RenderTestFrame();

        Assert.Equal(0, (int)parent.ScreenX);
        Assert.Equal(0, (int)parent.ScreenY);
        Assert.Equal(2, (int)child.ScreenX);
        Assert.Equal(2, (int)child.ScreenY);
    }

    [Fact]
    public void RenderableConstructor_PersistsInitialPositionOptions()
    {
        using var renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 40,
            Height = 20,
        });

        var box = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "box",
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(3),
            Top = DimensionValue.Point(4),
            Width = DimensionValue.Point(5),
            Height = DimensionValue.Point(3),
        });

        Assert.True(box.Left?.IsPoint);
        Assert.True(box.Top?.IsPoint);
        Assert.Equal(3, (int)box.Left!.Value.Value);
        Assert.Equal(4, (int)box.Top!.Value.Value);
    }

    [Fact]
    public void ResizeFromEastBorder_DoesNotMoveTopLeft()
    {
        using var renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 60,
            Height = 30,
        });

        const int minWidth = 10;
        const int minHeight = 5;

        BoxRenderable? contentBox = null;
        ScrollBoxRenderable? textBox = null;

        bool isResizing = false;
        string? resizeDirection = null;
        int resizeStartX = 0;
        int resizeStartY = 0;
        int resizeStartLeft = 0;
        int resizeStartTop = 0;
        int resizeStartWidth = 0;
        int resizeStartHeight = 0;
        TextWrapResizeRect? lastBounds = null;
        int lastDeltaX = 0;
        int lastDeltaY = 0;

        static string? GetResizeDirection(int mouseX, int mouseY, int boxLeft, int boxTop, int boxWidth, int boxHeight)
        {
            bool onLeftBorder = mouseX == boxLeft;
            bool onRightBorder = mouseX == boxLeft + boxWidth - 1;
            bool onTopBorder = mouseY == boxTop;
            bool onBottomBorder = mouseY == boxTop + boxHeight - 1;

            bool withinHorizontalBounds = mouseX >= boxLeft && mouseX <= boxLeft + boxWidth - 1;
            bool withinVerticalBounds = mouseY >= boxTop && mouseY <= boxTop + boxHeight - 1;

            bool left = onLeftBorder && withinVerticalBounds;
            bool right = onRightBorder && withinVerticalBounds;
            bool top = onTopBorder && withinHorizontalBounds;
            bool bottom = onBottomBorder && withinHorizontalBounds;

            if (top && left) return "nw";
            if (top && right) return "ne";
            if (bottom && left) return "sw";
            if (bottom && right) return "se";
            if (top) return "n";
            if (bottom) return "s";
            if (left) return "w";
            if (right) return "e";
            return null;
        }

        void HandleTextBoxMouse(UiMouseEvent mouseEvent)
        {
            if (textBox is null)
                return;

            switch (mouseEvent.Type)
            {
                case MouseEventType.Move:
                case MouseEventType.Over:
                    if (!isResizing)
                    {
                        resizeDirection = GetResizeDirection(
                            mouseEvent.X,
                            mouseEvent.Y,
                            (int)textBox.ScreenX,
                            (int)textBox.ScreenY,
                            textBox.Width,
                            textBox.Height);
                    }
                    break;

                case MouseEventType.Down:
                    if (resizeDirection is not null)
                    {
                        isResizing = true;
                        resizeStartX = mouseEvent.X;
                        resizeStartY = mouseEvent.Y;
                        resizeStartWidth = textBox.Width;
                        resizeStartHeight = textBox.Height;
                        resizeStartLeft = textBox.Left?.IsPoint == true ? (int)textBox.Left.Value.Value : 0;
                        resizeStartTop = textBox.Top?.IsPoint == true ? (int)textBox.Top.Value.Value : 0;
                        mouseEvent.StopPropagation();
                    }
                    break;
            }
        }

        void HandleGlobalMouse(UiMouseEvent mouseEvent)
        {
            if (mouseEvent.Type != MouseEventType.Drag || !isResizing || resizeDirection is null || textBox is null || contentBox is null)
                return;

            int deltaX = mouseEvent.X - resizeStartX;
            int deltaY = mouseEvent.Y - resizeStartY;
            lastDeltaX = deltaX;
            lastDeltaY = deltaY;
            var bounds = TextWrapDemoLogic.ComputeResizedBounds(
                resizeDirection,
                deltaX,
                deltaY,
                resizeStartLeft,
                resizeStartTop,
                resizeStartWidth,
                resizeStartHeight,
                minWidth,
                minHeight,
                contentBox.Width,
                contentBox.Height);
            lastBounds = bounds;

            textBox.WidthDimension = DimensionValue.Point(bounds.Width);
            textBox.HeightDimension = DimensionValue.Point(bounds.Height);
            textBox.Left = DimensionValue.Point(bounds.Left);
            textBox.Top = DimensionValue.Point(bounds.Top);
        }

        renderer.Root.OnMouse = HandleGlobalMouse;

        contentBox = new BoxRenderable(renderer, new BoxOptions
        {
            Id = "content-box",
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(0),
            Top = DimensionValue.Point(0),
            Width = DimensionValue.Point(40),
            Height = DimensionValue.Point(20),
            Border = true,
            Padding = DimensionValue.Point(1),
        });
        renderer.Root.Add(contentBox);

        textBox = new ScrollBoxRenderable(renderer, new ScrollBoxOptions
        {
            Id = "text-box",
            Position = PositionValue.Absolute,
            Left = DimensionValue.Point(TextWrapDemoLogic.InitialTextBoxInset),
            Top = DimensionValue.Point(TextWrapDemoLogic.InitialTextBoxInset),
            Width = DimensionValue.Point(20),
            Height = DimensionValue.Point(10),
            Border = true,
            OnMouse = HandleTextBoxMouse,
        });
        contentBox.Add(textBox);

        renderer.PresentTestFrame();

        Assert.Equal(TextWrapDemoLogic.InitialTextBoxInset, (int)textBox.Left!.Value.Value);
        Assert.Equal(TextWrapDemoLogic.InitialTextBoxInset, (int)textBox.Top!.Value.Value);
        Assert.Equal(2, (int)textBox.ScreenX);
        Assert.Equal(2, (int)textBox.ScreenY);

        int eastBorderX = (int)textBox.ScreenX + textBox.Width - 1;
        int middleY = (int)textBox.ScreenY + 1;

        renderer.DispatchTestMouseEvent(new RawMouseEvent
        {
            Type = MouseEventType.Move,
            Button = (int)MouseButton.Left,
            X = eastBorderX,
            Y = middleY,
            Modifiers = default,
        });
        renderer.DispatchTestMouseEvent(new RawMouseEvent
        {
            Type = MouseEventType.Down,
            Button = (int)MouseButton.Left,
            X = eastBorderX,
            Y = middleY,
            Modifiers = default,
        });

        Assert.Equal("e", resizeDirection);
        Assert.Equal(TextWrapDemoLogic.InitialTextBoxInset, resizeStartLeft);
        Assert.Equal(TextWrapDemoLogic.InitialTextBoxInset, resizeStartTop);

        renderer.DispatchTestMouseEvent(new RawMouseEvent
        {
            Type = MouseEventType.Drag,
            Button = (int)MouseButton.Left,
            X = eastBorderX + 1,
            Y = middleY,
            Modifiers = default,
        });
        renderer.PresentTestFrame();

        Assert.NotNull(lastBounds);
        Assert.Equal(1, lastDeltaX);
        Assert.Equal(0, lastDeltaY);
        Assert.Equal(TextWrapDemoLogic.InitialTextBoxInset, (int)textBox.Left!.Value.Value);
        Assert.Equal(TextWrapDemoLogic.InitialTextBoxInset, (int)textBox.Top!.Value.Value);
        Assert.Equal(2, (int)textBox.ScreenX);
        Assert.Equal(2, (int)textBox.ScreenY);
    }
}
