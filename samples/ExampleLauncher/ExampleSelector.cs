using System.Diagnostics;
using OpenTui.Core;

namespace ExampleLauncher;

/// <summary>
/// Full-screen TUI menu for browsing and launching sample executables.
/// Matches the TypeScript ExampleSelector from index.ts.
/// </summary>
internal sealed class ExampleSelector
{
    private readonly CliRenderer _renderer;
    private readonly List<SampleInfo> _allSamples;
    private readonly TaskCompletionSource<SampleInfo?> _selectedTcs = new();
    private readonly int _initialSelectedIndex;

    private BoxRenderable _menuContainer = null!;
    private ASCIIFontRenderable _title = null!;
    private BoxRenderable _filterBox = null!;
    private TextareaRenderable _filterInput = null!;
    private BoxRenderable _selectBox = null!;
    private SelectRenderable _selectElement = null!;
    private TimeToFirstDrawRenderable _timeToFirstDraw = null!;
    private TextRenderable _instructions = null!;

    // Theme colors (dark mode — matches TS MENU_THEMES.dark)
    private static readonly Rgba TitleColor = Rgba.FromInts(240, 248, 255, 255);
    private static readonly Rgba ThemeBorderColor = Rgba.FromHex("#475569");
    private static readonly Rgba ThemeFocusedBorderColor = Rgba.FromHex("#60A5FA");
    private static readonly Rgba InputTextColor = Rgba.FromHex("#E2E8F0");
    private static readonly Rgba InputPlaceholderColor = Rgba.FromHex("#94A3B8");
    private static readonly Rgba InputCursorColor = Rgba.FromHex("#60A5FA");
    private static readonly Rgba SelectSelectedBg = Rgba.FromHex("#1E3A5F");
    private static readonly Rgba ThemeSelectTextColor = Rgba.FromHex("#E2E8F0");
    private static readonly Rgba SelectSelectedText = Rgba.FromHex("#38BDF8");
    private static readonly Rgba ThemeDescriptionColor = Rgba.FromHex("#64748B");
    private static readonly Rgba SelectedDescColor = Rgba.FromHex("#94A3B8");
    private static readonly Rgba InstructionsColor = Rgba.FromHex("#94A3B8");

    public ExampleSelector(CliRenderer renderer, List<SampleInfo> samples, int initialSelectedIndex = 0)
    {
        _renderer = renderer;
        _allSamples = samples;
        _initialSelectedIndex = initialSelectedIndex;

        using (_renderer.SuspendRenderRequests())
        {
            CreateLayout();
        }
        SetupKeyboard();
    }

    /// <summary>
    /// Waits until the user selects a sample or quits (returns null).
    /// </summary>
    public Task<SampleInfo?> WaitForSelectionAsync() => _selectedTcs.Task;

    /// <summary>
    /// Removes all menu elements from the renderer tree so the next
    /// ExampleSelector can build a fresh layout on the same renderer.
    /// </summary>
    public void Cleanup()
    {
        _menuContainer.Destroy();
        _timeToFirstDraw.Reset();
    }

    private void CreateLayout()
    {
        int width = _renderer.Width;

        _menuContainer = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "menu-container",
            FlexDirection = FlexDirectionValue.Column,
            Width = DimensionValue.Percent(100),
            Height = DimensionValue.Percent(100),
        });
        _renderer.Root.Add(_menuContainer);

        // ASCII art title
        var titleText = "OPENTUI EXAMPLES";
        var titleFont = "tiny";
        var (titleWidth, _) = AsciiFont.MeasureText(titleText, titleFont);
        int centerX = Math.Max(0, width / 2 - titleWidth / 2);

        _title = new ASCIIFontRenderable(_renderer, new ASCIIFontOptions
        {
            Text = titleText,
            Font = titleFont,
            Color = TitleColor,
            BackgroundColor = Rgba.Transparent,
            Left = centerX,
            Margin = 1,
        });
        _menuContainer.Add(_title);

        // Filter box with border
        _filterBox = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "filter-box",
            MarginLeft = 1,
            MarginRight = 1,
            FlexShrink = 0,
            BackgroundColor = Rgba.Transparent,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = ThemeBorderColor,
        });
        _menuContainer.Add(_filterBox);

        // Filter text input
        _filterInput = new TextareaRenderable(_renderer, new TextareaOptions
        {
            Id = "filter-input",
            Width = DimensionValue.Percent(100),
            Height = DimensionValue.Point(1),
            Placeholder = "Filter examples by title...",
            PlaceholderColor = InputPlaceholderColor,
            BackgroundColor = Rgba.Transparent,
            FocusedBackgroundColor = Rgba.Transparent,
            TextColor = InputTextColor,
            FocusedTextColor = InputTextColor,
            WrapMode = 0, // 0 = none
            ShowCursor = true,
            CursorColor = InputCursorColor,
            OnContentChange = UpdateFilter,
        });
        _filterBox.Add(_filterInput);
        _filterInput.Focus();

        // Select box
        _selectBox = new BoxRenderable(_renderer, new BoxOptions
        {
            Id = "select-box",
            MarginLeft = 1,
            MarginRight = 1,
            MarginBottom = 1,
            FlexGrow = 1,
            Border = true,
            BorderStyle = BorderStyle.Single,
            BorderColor = ThemeBorderColor,
            FocusedBorderColor = ThemeFocusedBorderColor,
            Title = "Examples",
            TitleAlignment = TitleAlignment.Center,
            BackgroundColor = Rgba.Transparent,
            ShouldFill = true,
        });
        _menuContainer.Add(_selectBox);

        // Select element
        _selectElement = new SelectRenderable(_renderer, new SelectOptions
        {
            Id = "example-select",
            Height = DimensionValue.Percent(100),
            Options = BuildSelectOptions(_allSamples),
            BackgroundColor = Rgba.Transparent,
            FocusedBackgroundColor = Rgba.Transparent,
            SelectedBackgroundColor = SelectSelectedBg,
            TextColor = ThemeSelectTextColor,
            SelectedTextColor = SelectSelectedText,
            DescriptionColor = ThemeDescriptionColor,
            SelectedDescriptionColor = SelectedDescColor,
            ShowScrollIndicator = true,
            WrapSelection = true,
            ShowDescription = true,
            FastScrollStep = 5,
        });
        _selectBox.Add(_selectElement);

        if (_initialSelectedIndex > 0)
            _selectElement.SelectedIndex = _initialSelectedIndex;

        _selectElement.On<(int Index, SelectOption? Option)>(
            SelectRenderable.Events.ItemSelected,
            args =>
            {
                if (args.Option is { } opt) OnItemSelected(opt);
            });

        // Time-to-first-draw diagnostic (matches TS launcher)
        _timeToFirstDraw = new TimeToFirstDrawRenderable(_renderer, new TimeToFirstDrawOptions
        {
            Id = "time-to-first-draw",
            Fg = InstructionsColor,
        });
        _menuContainer.Add(_timeToFirstDraw);

        // Instructions bar
        _instructions = new TextRenderable(_renderer, new TextOptions
        {
            Id = "instructions",
            Height = DimensionValue.Point(1),
            FlexShrink = 0,
            AlignSelf = AlignValue.Center,
            Content = "Type to filter │ ↑↓/j/k navigate │ Enter/double-click run │ Esc clear/return │ Ctrl+C quit",
            Fg = InstructionsColor,
        });
        _menuContainer.Add(_instructions);
    }

    private void SetupKeyboard()
    {
        _renderer.KeyInput.On<KeyEvent>(KeyHandlerEvents.Keypress, key =>
        {
            if (key is { Name: "c", Ctrl: true })
            {
                _selectedTcs.TrySetResult(null);
                return;
            }

            // Ctrl+Z: suspend renderer (auto-resume after 5 seconds, matching TS launcher)
            if (key is { Name: "z", Ctrl: true })
            {
                _renderer.Suspend();
                Console.WriteLine("Renderer suspended. Resuming in 5 seconds...");
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    _renderer.Resume();
                });
                return;
            }

            // Forward navigation keys to select even when filter is focused
            if (_filterInput.Focused)
            {
                if (key.Name is "up" or "k")
                {
                    key.PreventDefault();
                    _selectElement.MoveUp(key.Shift ? 5 : 1);
                    return;
                }
                if (key.Name is "down" or "j")
                {
                    key.PreventDefault();
                    _selectElement.MoveDown(key.Shift ? 5 : 1);
                    return;
                }
                if (key.Name is "return" or "linefeed")
                {
                    key.PreventDefault();
                    _selectElement.SelectCurrent();
                    return;
                }
            }

            // Escape clears filter if non-empty
            if (key.Name is "escape")
            {
                var text = _filterInput.EditBuffer.GetText();
                if (text.Length > 0)
                {
                    key.PreventDefault();
                    _filterInput.SetText("");
                    UpdateFilter();
                }
            }
        });
    }

    private void UpdateFilter()
    {
        var filterText = _filterInput.EditBuffer.GetText().Trim();

        if (filterText.Length == 0)
        {
            _selectElement.Options = BuildSelectOptions(_allSamples);
        }
        else
        {
            var filtered = _allSamples
                .Where(s => s.DisplayName.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _selectElement.Options = BuildSelectOptions(filtered);
        }
    }

    private void OnItemSelected(SelectOption option)
    {
        if (option.Value is SampleInfo sample)
            _selectedTcs.TrySetResult(sample);
    }

    private static SelectOption[] BuildSelectOptions(List<SampleInfo> samples) =>
        samples.Select(s => new SelectOption
        {
            Name = s.DisplayName,
            Description = s.Description.Length > 0 ? s.Description : null,
            Value = s,
        }).ToArray();

    /// <summary>
    /// Spawns a sample executable, waits for it to exit, and returns.
    /// The caller is responsible for tearing down and re-creating the renderer.
    /// </summary>
    public static void RunSample(SampleInfo sample)
    {
        var psi = new ProcessStartInfo
        {
            FileName = sample.ExePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(sample.ExePath),
        };

        using var process = Process.Start(psi);
        process?.WaitForExit();
    }
}
