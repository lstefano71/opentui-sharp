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

    [Fact]
    public void Create_SplitFooterConfig_UsesFooterRenderSize()
    {
        using var renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
            ScreenMode = ScreenMode.SplitFooter,
            FooterHeight = 6,
            ExternalOutputMode = ExternalOutputMode.CaptureStdout,
        });

        Assert.Equal(ScreenMode.SplitFooter, renderer.ScreenMode);
        Assert.Equal(ExternalOutputMode.CaptureStdout, renderer.ExternalOutputMode);
        Assert.Equal(80, renderer.Width);
        Assert.Equal(6, renderer.Height);
        Assert.Equal(80, renderer.TerminalWidth);
        Assert.Equal(24, renderer.TerminalHeight);
    }

    [Fact]
    public void Create_RejectsCapturedOutputOutsideSplitFooter()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
            ScreenMode = ScreenMode.MainScreen,
            ExternalOutputMode = ExternalOutputMode.CaptureStdout,
        }));

        Assert.Equal("externalOutputMode \"CaptureStdout\" requires screenMode \"SplitFooter\".", exception.Message);
    }

    [Fact]
    public void Create_ExposesTerminalCapabilities()
    {
        Assert.IsType<TerminalCapabilities>(_renderer.Capabilities);
    }

    [Fact]
    public void Create_ExposesKittyKeyboardUsage()
    {
        Assert.True(_renderer.UseKittyKeyboard);
    }

    [Fact]
    public void Create_HasConsoleOverlay()
    {
        Assert.NotNull(_renderer.Console);
    }

    [Fact]
    public void SuspendRenderRequests_DefersAndFlushesDeferredRequest()
    {
        using (var suspension = _renderer.SuspendRenderRequests())
        {
            Assert.Equal(1, _renderer.RenderRequestSuspensionCount);

            _renderer.RequestRender();

            Assert.True(_renderer.HasDeferredRenderRequest);
        }

        Assert.Equal(0, _renderer.RenderRequestSuspensionCount);
        Assert.False(_renderer.HasDeferredRenderRequest);
    }

    [Fact]
    public void Resize_SplitFooterPreservesFooterHeight()
    {
        using var renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
            ScreenMode = ScreenMode.SplitFooter,
            FooterHeight = 6,
            ExternalOutputMode = ExternalOutputMode.CaptureStdout,
        });

        renderer.Resize(100, 30);

        Assert.Equal(100, renderer.Width);
        Assert.Equal(6, renderer.Height);
        Assert.Equal(100, renderer.TerminalWidth);
        Assert.Equal(30, renderer.TerminalHeight);
    }

    [Fact]
    public void ScreenMode_LeavingSplitFooterFlushesCapturedOutput()
    {
        using var renderer = CliRenderer.Create(new CliRendererConfig
        {
            Testing = true,
            Width = 80,
            Height = 24,
            ScreenMode = ScreenMode.SplitFooter,
            FooterHeight = 6,
            ExternalOutputMode = ExternalOutputMode.CaptureStdout,
        });

        renderer.CaptureExternalOutput("pending output\n");
        Assert.True(renderer.CapturedOutputLength > 0);

        renderer.ExternalOutputMode = ExternalOutputMode.Passthrough;
        renderer.ScreenMode = ScreenMode.MainScreen;

        Assert.Equal(0, renderer.CapturedOutputLength);
    }

    [Fact]
    public void Console_Show_UsesBottomBoundsAndFocuses()
    {
        var console = _renderer.Console;

        console.Show();

        Assert.True(console.Visible);
        Assert.True(console.Focused);
        Assert.Equal(ConsolePosition.Bottom, console.Position);
        Assert.Equal((0, 17, 80, 7), console.Bounds);
    }

    [Fact]
    public void Console_DirectLogsAreStoredWithLevels()
    {
        var console = _renderer.Console;

        console.Log("alpha");
        console.Warn("beta");

        Assert.Collection(
            console.Entries,
            entry =>
            {
                Assert.Equal(ConsoleLogLevel.Log, entry.Level);
                Assert.Equal("alpha", entry.Text);
            },
            entry =>
            {
                Assert.Equal(ConsoleLogLevel.Warn, entry.Level);
                Assert.Equal("beta", entry.Text);
            });
    }

    [Fact]
    public void Console_KeyBindingsCyclePositionAndSize()
    {
        var console = _renderer.Console;
        console.Show();

        _renderer.DispatchTestKeyInput(new ParsedKey
        {
            Name = "p",
            Raw = "\x10",
            Ctrl = true,
        });

        Assert.Equal(ConsolePosition.Right, console.Position);

        _renderer.DispatchTestKeyInput(new ParsedKey
        {
            Name = "+",
            Raw = "+",
        });

        Assert.Equal(35, console.SizePercent);
        Assert.Equal((52, 0, 28, 24), console.Bounds);
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

    [Fact]
    public void DispatchTestResponse_ThemeModeSequence_UpdatesThemeModeAndEmitsEvent()
    {
        ThemeMode? emitted = null;
        _renderer.On<ThemeMode>(RendererEventNames.ThemeMode, mode => emitted = mode);

        _renderer.DispatchTestResponse("\x1b[?997;2n");

        Assert.Equal(ThemeMode.Light, _renderer.TerminalThemeMode);
        Assert.Equal(ThemeMode.Light, emitted);
    }

    [Fact]
    public async Task GetPalette_ParsesOscResponsesAndCachesResult()
    {
        var paletteTask = _renderer.GetPalette(new GetPaletteOptions { Size = 2, Timeout = 250 });

        Assert.Equal("detecting", _renderer.PaletteDetectionStatus);

        _renderer.DispatchTestResponse("\x1b]4;0;rgb:11/22/33\x07", "osc");
        _renderer.DispatchTestResponse("\x1b]4;1;#445566\x07", "osc");
        _renderer.DispatchTestResponse("\x1b]10;#abcdef\x07", "osc");
        _renderer.DispatchTestResponse("\x1b]11;#123456\x07", "osc");
        _renderer.DispatchTestResponse("\x1b]12;#654321\x07", "osc");
        _renderer.DispatchTestResponse("\x1b]13;#111111\x07", "osc");
        _renderer.DispatchTestResponse("\x1b]14;#222222\x07", "osc");
        _renderer.DispatchTestResponse("\x1b]15;#333333\x07", "osc");
        _renderer.DispatchTestResponse("\x1b]16;#444444\x07", "osc");
        _renderer.DispatchTestResponse("\x1b]17;#555555\x07", "osc");
        _renderer.DispatchTestResponse("\x1b]19;#666666\x07", "osc");

        var colors = await paletteTask;

        Assert.Equal(["#112233", "#445566"], colors.Palette);
        Assert.Equal("#abcdef", colors.DefaultForeground);
        Assert.Equal("#123456", colors.DefaultBackground);
        Assert.Equal("#654321", colors.CursorColor);
        Assert.Equal("cached", _renderer.PaletteDetectionStatus);

        _renderer.ClearPaletteCache();

        Assert.Equal("idle", _renderer.PaletteDetectionStatus);
    }

    [Fact]
    public void InputHandlers_HandledRawKeySuppressesParsedKeyDispatch()
    {
        int keypressCount = 0;
        _renderer.KeyInput.On<KeyEvent>(KeyHandlerEvents.Keypress, _ => keypressCount++);
        _renderer.AddInputHandler(_ => true);

        _renderer.DispatchTestKeyInput(new ParsedKey
        {
            Name = "a",
            Sequence = "a",
            Raw = "a",
        });

        Assert.Equal(0, keypressCount);
    }

    [Fact]
    public void DebugInputs_KeyDispatchIsCapturedWhenDebugModeEnabled()
    {
        _renderer.SetDebugMode(true);

        _renderer.DispatchTestKeyInput(new ParsedKey
        {
            Name = "a",
            Sequence = "a",
            Raw = "a",
        });

        var records = _renderer.GetDebugInputs();
        var record = Assert.Single(records);
        Assert.Equal("a", record.Sequence);
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
