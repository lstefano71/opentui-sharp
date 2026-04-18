using Xunit;

namespace OpenTui.Core.Tests;

/// <summary>
/// Tests for CliRenderer — the concrete IRenderContext implementation.
/// All tests use testing=true mode (no terminal setup, no stdin).
/// </summary>
public sealed class CliRendererTests : IDisposable
{
    private readonly CliRenderer _renderer;

    public CliRendererTests()
    {
        _renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
        });
    }

    public void Dispose() => _renderer.Dispose();

    #region Creation & Properties

    [Fact]
    public void Create_SetsWidthAndHeight()
    {
        Assert.Equal(80, _renderer.Width);
        Assert.Equal(24, _renderer.Height);
    }

    [Fact]
    public void Create_HasNonNullRoot()
    {
        Assert.NotNull(_renderer.Root);
    }

    [Fact]
    public void Create_HasNonNullBuffers()
    {
        Assert.NotNull(_renderer.NextRenderBuffer);
        Assert.NotNull(_renderer.CurrentRenderBuffer);
    }

    [Fact]
    public void Create_FrameIdStartsAtZero()
    {
        Assert.Equal(0, _renderer.FrameId);
    }

    [Fact]
    public void Create_IsNotDestroyed()
    {
        Assert.False(_renderer.IsDestroyed);
    }

    [Fact]
    public void Create_IsNotRunning()
    {
        Assert.False(_renderer.IsRunning);
    }

    [Fact]
    public void Create_WidthMethodIsUnicode()
    {
        Assert.Equal(WidthMethod.Unicode, _renderer.WidthMethod);
    }

    [Fact]
    public void Create_HasKeyInput()
    {
        Assert.NotNull(_renderer.KeyInput);
        Assert.Same(_renderer.KeyInput, _renderer.InternalKeyInput);
    }

    #endregion

    #region Focus

    [Fact]
    public void Focus_InitiallyNull()
    {
        Assert.Null(_renderer.CurrentFocusedRenderable);
    }

    [Fact]
    public void Focus_BlurRenderable_WhenNotFocused_DoesNothing()
    {
        var child = new CliTestRenderable(_renderer);
        _renderer.Root.Add(child);
        _renderer.BlurRenderable(child);
        Assert.Null(_renderer.CurrentFocusedRenderable);
    }

    [Fact]
    public void Focus_FocusRenderable_SetsCurrent()
    {
        var child = new CliTestRenderable(_renderer) { Focusable = true };
        _renderer.Root.Add(child);
        _renderer.FocusRenderable(child);
        Assert.Same(child, _renderer.CurrentFocusedRenderable);
    }

    [Fact]
    public void Focus_BlurRenderable_ClearsCurrent()
    {
        var child = new CliTestRenderable(_renderer) { Focusable = true };
        _renderer.Root.Add(child);
        _renderer.FocusRenderable(child);
        _renderer.BlurRenderable(child);
        Assert.Null(_renderer.CurrentFocusedRenderable);
    }

    [Fact]
    public void Focus_FocusSecond_BlursPrevious()
    {
        var a = new CliTestRenderable(_renderer) { Focusable = true };
        var b = new CliTestRenderable(_renderer) { Focusable = true };
        _renderer.Root.Add(a);
        _renderer.Root.Add(b);

        _renderer.FocusRenderable(a);
        Assert.Same(a, _renderer.CurrentFocusedRenderable);

        _renderer.FocusRenderable(b);
        Assert.Same(b, _renderer.CurrentFocusedRenderable);
    }

    #endregion

    #region Selection

    [Fact]
    public void Selection_InitiallyNone()
    {
        Assert.False(_renderer.HasSelection);
        Assert.Null(_renderer.GetSelection());
    }

    [Fact]
    public void Selection_StartAndGet()
    {
        var child = new CliTestRenderable(_renderer);
        child.Selectable = true;
        _renderer.Root.Add(child);

        _renderer.StartSelection(child, 5, 10);
        Assert.True(_renderer.HasSelection);

        var sel = _renderer.GetSelection();
        Assert.NotNull(sel);
    }

    [Fact]
    public void Selection_ClearSelection()
    {
        var child = new CliTestRenderable(_renderer);
        child.Selectable = true;
        _renderer.Root.Add(child);

        _renderer.StartSelection(child, 5, 10);
        Assert.True(_renderer.HasSelection);

        _renderer.ClearSelection();
        Assert.False(_renderer.HasSelection);
    }

    [Fact]
    public void Selection_StartSelection_NotifiesSelectableRenderable()
    {
        var child = new SelectableCliTestRenderable(_renderer, "alpha");
        _renderer.Root.Add(child);

        _renderer.StartSelection(child, 1, 1);

        var selection = Assert.IsType<Selection>(_renderer.GetSelection());
        Assert.True(selection.IsStart);
        Assert.Contains(child, selection.SelectedRenderables);
        Assert.Equal(selection, child.LastSelection);
        Assert.Equal("alpha", selection.GetSelectedText());
    }

    [Fact]
    public void Selection_ClearSelection_NotifiesTouchedRenderableWithNull()
    {
        var child = new SelectableCliTestRenderable(_renderer, "alpha");
        _renderer.Root.Add(child);

        _renderer.StartSelection(child, 1, 1);
        _renderer.ClearSelection();

        Assert.Null(_renderer.GetSelection());
        Assert.Null(child.LastSelection);
        Assert.True(child.NullSelectionNotificationCount > 0);
    }

    [Fact]
    public void MouseCapture_DragKeepsSendingEventsToCapturedRenderable_AndDropsOnTarget()
    {
        var source = new MouseTrackingRenderable(_renderer, "source")
        {
            PositionType = PositionValue.Absolute,
            Left = DimensionValue.Point(1),
            Top = DimensionValue.Point(1),
            WidthDimension = DimensionValue.Point(10),
            HeightDimension = DimensionValue.Point(4),
        };
        var target = new MouseTrackingRenderable(_renderer, "target")
        {
            PositionType = PositionValue.Absolute,
            Left = DimensionValue.Point(20),
            Top = DimensionValue.Point(1),
            WidthDimension = DimensionValue.Point(10),
            HeightDimension = DimensionValue.Point(4),
        };

        _renderer.Root.Add(source);
        _renderer.Root.Add(target);
        _renderer.PresentTestFrame();

        _renderer.DispatchTestMouseEvent(new RawMouseEvent
        {
            Type = MouseEventType.Down,
            Button = (int)MouseButton.Left,
            X = 2,
            Y = 2,
            Modifiers = default,
        });
        _renderer.DispatchTestMouseEvent(new RawMouseEvent
        {
            Type = MouseEventType.Drag,
            Button = (int)MouseButton.Left,
            X = 3,
            Y = 2,
            Modifiers = default,
        });
        _renderer.DispatchTestMouseEvent(new RawMouseEvent
        {
            Type = MouseEventType.Drag,
            Button = (int)MouseButton.Left,
            X = 21,
            Y = 2,
            Modifiers = default,
        });
        _renderer.DispatchTestMouseEvent(new RawMouseEvent
        {
            Type = MouseEventType.Up,
            Button = (int)MouseButton.Left,
            X = 21,
            Y = 2,
            Modifiers = default,
        });

        Assert.Equal(1, source.MouseDownCount);
        Assert.Equal(1, source.MouseUpCount);
        Assert.Equal(2, source.MouseDragCount);
        Assert.Equal(1, source.MouseDragEndCount);
        Assert.Equal(1, target.MouseDropCount);
        Assert.Equal("source", target.LastDropSourceId);
    }

    #endregion

    #region Terminal Focus

    [Fact]
    public void TerminalFocus_FocusResponse_EmitsFocusAndUpdatesState()
    {
        int focusCount = 0;
        _renderer.On(RendererEventNames.Focus, () => focusCount++);

        _renderer.DispatchTestResponse("\x1b[I");

        Assert.Equal(1, focusCount);
        Assert.True(_renderer.TerminalFocusState);
        Assert.False(_renderer.ShouldRestoreModesOnNextFocus);
    }

    [Fact]
    public void TerminalFocus_BlurThenFocus_TogglesStateAndEmitsOncePerTransition()
    {
        int focusCount = 0;
        int blurCount = 0;
        _renderer.On(RendererEventNames.Focus, () => focusCount++);
        _renderer.On(RendererEventNames.Blur, () => blurCount++);

        _renderer.DispatchTestResponse("\x1b[O");
        _renderer.DispatchTestResponse("\x1b[O");
        _renderer.DispatchTestResponse("\x1b[I");
        _renderer.DispatchTestResponse("\x1b[I");

        Assert.Equal(1, blurCount);
        Assert.Equal(1, focusCount);
        Assert.True(_renderer.TerminalFocusState);
        Assert.False(_renderer.ShouldRestoreModesOnNextFocus);
    }

    #endregion

    #region Lifecycle Passes

    [Fact]
    public void LifecyclePasses_InitiallyEmpty()
    {
        Assert.Empty(_renderer.GetLifecyclePasses());
    }

    [Fact]
    public void LifecyclePasses_RegisterAndUnregister()
    {
        var child = new CliTestRenderable(_renderer);
        _renderer.Root.Add(child);

        _renderer.RegisterLifecyclePass(child);
        Assert.Contains(child, _renderer.GetLifecyclePasses());

        _renderer.UnregisterLifecyclePass(child);
        Assert.DoesNotContain(child, _renderer.GetLifecyclePasses());
    }

    #endregion

    #region Resize

    [Fact]
    public void Resize_UpdatesDimensions()
    {
        _renderer.Resize(120, 40);
        Assert.Equal(120, _renderer.Width);
        Assert.Equal(40, _renderer.Height);
    }

    [Fact]
    public void Resize_SameDimensions_IsNoop()
    {
        var buf = _renderer.NextRenderBuffer;
        _renderer.Resize(80, 24); // same as initial
        Assert.Same(buf, _renderer.NextRenderBuffer);
    }

    [Fact]
    public void Resize_EmitsResizeEvent()
    {
        bool resized = false;
        _renderer.On<(int W, int H)>(RendererEventNames.Resize, e => resized = true);
        _renderer.Resize(100, 50);
        Assert.True(resized);
    }

    [Fact]
    public void PostProcess_AddAndClearControlsExecution()
    {
        int runs = 0;
        void PostProcess(OptimizedBuffer _, float __) => runs++;

        _renderer.AddPostProcessFn(PostProcess);
        _renderer.PresentTestFrame();
        Assert.Equal(1, runs);

        _renderer.ClearPostProcessFns();
        _renderer.PresentTestFrame();
        Assert.Equal(1, runs);
    }

    #endregion

    #region Destroy

    [Fact]
    public void Destroy_SetsIsDestroyed()
    {
        _renderer.Destroy();
        Assert.True(_renderer.IsDestroyed);
    }

    [Fact]
    public void Destroy_EmitsDestroyEvent()
    {
        bool destroyed = false;
        _renderer.On(RendererEventNames.Destroy, () => destroyed = true);
        _renderer.Destroy();
        Assert.True(destroyed);
    }

    [Fact]
    public void Destroy_DoubleDestroy_DoesNotThrow()
    {
        _renderer.Destroy();
        _renderer.Destroy(); // should not throw
    }

    #endregion

    #region Render Loop (testing mode)

    [Fact]
    public void RequestRender_InTestingMode_DoesNotThrow()
    {
        _renderer.RequestRender();
    }

    [Fact]
    public void RequestRender_AfterDestroy_IsNoop()
    {
        _renderer.Destroy();
        _renderer.RequestRender(); // should not throw
    }

    #endregion

    #region Live Mode

    [Fact]
    public void LiveMode_RequestAndDrop()
    {
        Assert.Equal(0, _renderer.LiveRequestCount);
        Assert.Equal("idle", _renderer.CurrentControlState);

        _renderer.RequestLive();
        Assert.True(_renderer.IsRunning);
        Assert.Equal(1, _renderer.LiveRequestCount);
        Assert.Equal("auto_started", _renderer.CurrentControlState);

        _renderer.DropLive();
        Assert.False(_renderer.IsRunning);
        Assert.Equal(0, _renderer.LiveRequestCount);
        Assert.Equal("idle", _renderer.CurrentControlState);
    }

    [Fact]
    public void LiveMode_DropBelowZero_Floors()
    {
        _renderer.DropLive();
        _renderer.DropLive(); // should not go below 0 or throw
        Assert.False(_renderer.IsRunning);
        Assert.Equal(0, _renderer.LiveRequestCount);
        Assert.Equal("idle", _renderer.CurrentControlState);
    }

    [Fact]
    public void LiveMode_MultipleRequests_RequiresMultipleDrops()
    {
        _renderer.RequestLive();
        _renderer.RequestLive();
        _renderer.DropLive();
        Assert.True(_renderer.IsRunning);

        _renderer.DropLive();
        Assert.False(_renderer.IsRunning);
    }

    [Fact]
    public void FrameBuffer_RespectAlphaOptionPropagatesToBuffer()
    {
        var frameBuffer = new FrameBufferRenderable(_renderer, new FrameBufferOptions
        {
            Id = "fb",
            Position = PositionValue.Absolute,
            Left = 0,
            Top = 0,
            Width = 4,
            Height = 2,
            RespectAlpha = false,
        });

        _renderer.Root.Add(frameBuffer);
        _renderer.PresentTestFrame();

        Assert.NotNull(frameBuffer.Buffer);
        Assert.False(frameBuffer.Buffer!.RespectAlpha);
    }

    #endregion

    #region Hit Grid

    [Fact]
    public void HitGrid_AddAndScissor_DoNotThrow()
    {
        _renderer.AddToHitGrid(0, 0, 10, 10, 1);
        _renderer.PushHitGridScissorRect(0, 0, 5, 5);
        _renderer.PopHitGridScissorRect();
        _renderer.ClearHitGridScissorRects();
    }

    #endregion

    #region Cursor

    [Fact]
    public void Cursor_SetPosition_DoesNotThrow()
    {
        _renderer.SetCursorPosition(5, 5, true);
    }

    [Fact]
    public void Cursor_SetColor_DoesNotThrow()
    {
        _renderer.SetCursorColor(Rgba.FromInts(255, 0, 0));
    }

    #endregion

    /// <summary>Minimal test renderable for CliRenderer tests.</summary>
    private sealed class CliTestRenderable : Renderable
    {
        public CliTestRenderable(IRenderContext ctx, RenderableOptions? options = null)
            : base(ctx, options ?? new RenderableOptions()) { }
        protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime) { }
    }

    private sealed class SelectableCliTestRenderable : Renderable
    {
        public SelectableCliTestRenderable(IRenderContext ctx, string selectedText)
            : base(ctx, new RenderableOptions
            {
                Width = DimensionValue.Point(12),
                Height = DimensionValue.Point(2),
            })
        {
            Selectable = true;
            _selectedText = selectedText;
        }

        private readonly string _selectedText;
        public Selection? LastSelection { get; private set; }
        public int NullSelectionNotificationCount { get; private set; }

        public override bool ShouldStartSelection(int x, int y) => true;

        public override bool OnSelectionChanged(Selection? selection)
        {
            LastSelection = selection;
            if (selection is null)
            {
                NullSelectionNotificationCount++;
                return false;
            }

            return true;
        }

        public override string GetSelectedText() => _selectedText;

        protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime) { }
    }

    private sealed class MouseTrackingRenderable : Renderable
    {
        public MouseTrackingRenderable(IRenderContext ctx, string id)
            : base(ctx, new RenderableOptions { Id = id }) { }

        public int MouseDownCount { get; private set; }
        public int MouseUpCount { get; private set; }
        public int MouseDragCount { get; private set; }
        public int MouseDragEndCount { get; private set; }
        public int MouseDropCount { get; private set; }
        public string? LastDropSourceId { get; private set; }

        protected override void OnMouseEvent(UiMouseEvent evt)
        {
            switch (evt.Type)
            {
                case MouseEventType.Down:
                    MouseDownCount++;
                    break;
                case MouseEventType.Up:
                    MouseUpCount++;
                    break;
                case MouseEventType.Drag:
                    MouseDragCount++;
                    break;
                case MouseEventType.DragEnd:
                    MouseDragEndCount++;
                    break;
                case MouseEventType.Drop:
                    MouseDropCount++;
                    LastDropSourceId = evt.Source;
                    break;
            }
        }

        protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime) { }
    }
}
