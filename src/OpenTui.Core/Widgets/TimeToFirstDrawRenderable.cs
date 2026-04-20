using System.Diagnostics;

namespace OpenTui.Core;

/// <summary>
/// Configuration options for <see cref="TimeToFirstDrawRenderable"/>.
/// </summary>
public sealed class TimeToFirstDrawOptions : RenderableOptions
{
    /// <summary>Gets or sets the text color. Defaults to #AAAAAA.</summary>
    public Rgba? Fg { get; init; }

    /// <summary>Gets or sets the label prefix. Defaults to "Time to first draw".</summary>
    public string? Label { get; init; }

    /// <summary>Gets or sets the decimal precision. Defaults to 2.</summary>
    public int? Precision { get; init; }
}

/// <summary>
/// Diagnostic renderable that captures and displays the time elapsed from process
/// start to its first render. Matches TypeScript TimeToFirstDrawRenderable.
/// </summary>
public sealed class TimeToFirstDrawRenderable : Renderable
{
    private static readonly long s_processStartTimestamp = GetProcessStartTimestamp();

    private double? _runtimeMs;
    private Rgba _textColor;
    private string _label;
    private int _precision;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeToFirstDrawRenderable"/> class.
    /// </summary>
    public TimeToFirstDrawRenderable(IRenderContext ctx, TimeToFirstDrawOptions? options = null)
        : base(ctx, MergeDefaults(options))
    {
        options ??= new TimeToFirstDrawOptions();
        _textColor = options.Fg ?? Rgba.FromHex("#AAAAAA");
        _label = options.Label ?? "Time to first draw";
        _precision = NormalizePrecision(options.Precision ?? 2);
    }

    /// <summary>Gets the captured runtime in milliseconds, or null if not yet rendered.</summary>
    public double? RuntimeMs => _runtimeMs;

    /// <summary>Sets the text color.</summary>
    public Rgba Fg
    {
        set { _textColor = value; RequestRender(); }
    }

    /// <summary>Sets the label prefix.</summary>
    public string TextLabel
    {
        set
        {
            if (value == _label) return;
            _label = value;
            RequestRender();
        }
    }

    /// <summary>Sets the decimal precision.</summary>
    public int Decimals
    {
        set
        {
            var next = NormalizePrecision(value);
            if (next == _precision) return;
            _precision = next;
            RequestRender();
        }
    }

    /// <summary>Clears the captured time so it re-measures on the next render.</summary>
    public void Reset()
    {
        _runtimeMs = null;
        RequestRender();
    }

    /// <inheritdoc/>
    protected override void RenderSelf(OptimizedBuffer buffer, float deltaTime)
    {
        if (_runtimeMs is null)
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - s_processStartTimestamp;
            _runtimeMs = (double)elapsedTicks / Stopwatch.Frequency * 1000.0;
        }

        var content = $"{_label}: {_runtimeMs.Value.ToString($"F{_precision}")}ms";
        int maxWidth = Math.Max(Width, 1);
        var visibleContent = content.Length > maxWidth ? content[..maxWidth] : content;
        int centeredX = X + Math.Max(0, (maxWidth - visibleContent.Length) / 2);

        buffer.DrawText(visibleContent, (uint)centeredX, (uint)Y, _textColor);
    }

    private static RenderableOptions MergeDefaults(TimeToFirstDrawOptions? options)
    {
        return new RenderableOptions
        {
            Id = options?.Id,
            Width = options?.Width ?? DimensionValue.Percent(100),
            Height = options?.Height ?? DimensionValue.Point(1),
            FlexShrink = options?.FlexShrink ?? 0,
            AlignSelf = options?.AlignSelf ?? AlignValue.Center,
            Visible = options?.Visible ?? true,
            ZIndex = options?.ZIndex ?? 0,
        };
    }

    private static int NormalizePrecision(int value) =>
        Math.Max(0, value);

    private static long GetProcessStartTimestamp()
    {
        // Use process start time to establish a baseline matching TS performance.now()
        var processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var offset = DateTime.UtcNow - processStart;
        return Stopwatch.GetTimestamp() - (long)(offset.TotalSeconds * Stopwatch.Frequency);
    }
}
