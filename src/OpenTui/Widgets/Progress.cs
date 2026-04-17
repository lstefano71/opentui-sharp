namespace OpenTui;

/// <summary>
/// A progress bar widget that fills proportionally based on
/// <see cref="Value"/> relative to <see cref="MaxValue"/>.
/// </summary>
public class Progress : Widget
{
    /// <summary>Current progress value.</summary>
    public double Value { get; set; }

    /// <summary>The value that represents 100% completion.</summary>
    public double MaxValue { get; set; } = 100;

    /// <summary>Computed percentage (0–100).</summary>
    public double Percentage => MaxValue > 0 ? Value / MaxValue * 100 : 0;

    /// <summary>Color for the completed portion of the bar.</summary>
    public Rgba? CompletedColor { get; set; }

    /// <summary>Color for the remaining portion of the bar.</summary>
    public Rgba? RemainingColor { get; set; }

    /// <summary>Whether to render the percentage label inside/beside the bar.</summary>
    public bool ShowPercentage { get; set; } = true;

    /// <summary>Optional label displayed alongside the progress bar.</summary>
    public string? Label { get; set; }

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}

/// <summary>
/// An animated spinner widget that cycles through Unicode frames
/// to indicate an ongoing operation.
/// </summary>
public class Spinner : Widget
{
    private static readonly string[] DotFrames =
        ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    /// <summary>The animation frames to cycle through.</summary>
    public string[] Frames { get; set; } = DotFrames;

    /// <summary>Index of the currently displayed frame.</summary>
    public int FrameIndex { get; set; }

    /// <summary>Optional text label rendered next to the spinner.</summary>
    public string? Label { get; set; }

    /// <summary>Color of the spinner character.</summary>
    public Rgba? SpinnerColor { get; set; }

    /// <summary>Advances to the next animation frame.</summary>
    public void Advance() => FrameIndex = (FrameIndex + 1) % Frames.Length;

    /// <summary>The character(s) for the current animation frame.</summary>
    public string CurrentFrame => Frames[FrameIndex % Frames.Length];

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
