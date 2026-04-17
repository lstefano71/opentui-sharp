namespace OpenTui;

/// <summary>
/// A resizable two-panel layout that splits available space between a
/// <see cref="First"/> and <see cref="Second"/> widget.
/// </summary>
public class SplitPane : Widget
{
    /// <summary>Split direction.</summary>
    public SplitOrientation Orientation { get; set; } = SplitOrientation.Horizontal;

    private float _splitRatio = 0.5f;

    /// <summary>
    /// Proportion of space allocated to the first panel (0.0–1.0),
    /// clamped between <see cref="MinRatio"/> and <see cref="MaxRatio"/>.
    /// </summary>
    public float SplitRatio
    {
        get => _splitRatio;
        set
        {
            _splitRatio = Math.Clamp(value, MinRatio, MaxRatio);
            OnSplitChanged?.Invoke(_splitRatio);
        }
    }

    /// <summary>Minimum allowed ratio for the first panel.</summary>
    public float MinRatio { get; set; } = 0.1f;

    /// <summary>Maximum allowed ratio for the first panel.</summary>
    public float MaxRatio { get; set; } = 0.9f;

    /// <summary>The first (left or top) panel widget.</summary>
    public Widget? First { get; set; }

    /// <summary>The second (right or bottom) panel widget.</summary>
    public Widget? Second { get; set; }

    /// <summary>Raised when the split ratio changes.</summary>
    public event Action<float>? OnSplitChanged;

    /// <inheritdoc />
    protected internal override void Draw(nint buffer, int offsetX, int offsetY)
    {
        // TODO: wire to native buffer
    }
}
