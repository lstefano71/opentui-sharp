namespace OpenTui.Core;

/// <summary>
/// Darkens the edges of a buffer using a precomputed per-cell attenuation mask.
/// Ported from the upstream VignetteEffect implementation.
/// </summary>
public sealed class VignetteEffect
{
    private static readonly float[] ZeroMatrix =
    [
        0, 0, 0, 0,
        0, 0, 0, 0,
        0, 0, 0, 0,
        0, 0, 0, 0,
    ];

    private float _strength;
    private float[]? _precomputedAttenuationCellMask;
    private int _cachedWidth = -1;
    private int _cachedHeight = -1;

    /// <summary>
    /// Initializes a new instance of the VignetteEffect class.
    /// </summary>
    /// <param name="strength">The strength.</param>
    public VignetteEffect(float strength = 0.5f)
    {
        Strength = strength;
    }

    /// <summary>
    /// Gets or sets the strength.
    /// </summary>
    public float Strength
    {
        get => _strength;
        set
        {
            _strength = Math.Max(0f, value);
            _cachedWidth = -1;
            _cachedHeight = -1;
            _precomputedAttenuationCellMask = null;
        }
    }

    /// <summary>
    /// Performs apply.
    /// </summary>
    /// <param name="buffer">The target buffer.</param>
    /// <param name="_">The .</param>
    public void Apply(OptimizedBuffer buffer, float _)
    {
        int width = (int)buffer.Width;
        int height = (int)buffer.Height;

        if (width != _cachedWidth || height != _cachedHeight || _precomputedAttenuationCellMask is null)
            ComputeFactors(width, height);

        buffer.ColorMatrix(ZeroMatrix, _precomputedAttenuationCellMask!, 1f, TargetChannel.Both);
    }

    private void ComputeFactors(int width, int height)
    {
        _precomputedAttenuationCellMask = new float[width * height * 3];

        float centerX = width / 2f;
        float centerY = height / 2f;
        float maxDistSq = centerX * centerX + centerY * centerY;
        float safeMaxDistSq = maxDistSq == 0f ? 1f : maxDistSq;

        int index = 0;
        for (int y = 0; y < height; y++)
        {
            float dy = y - centerY;
            float dySq = dy * dy;
            for (int x = 0; x < width; x++)
            {
                float dx = x - centerX;
                float distSq = dx * dx + dySq;
                float baseAttenuation = Math.Min(1f, distSq / safeMaxDistSq);
                float attenuation = baseAttenuation * _strength;

                _precomputedAttenuationCellMask[index++] = x;
                _precomputedAttenuationCellMask[index++] = y;
                _precomputedAttenuationCellMask[index++] = attenuation;
            }
        }

        _cachedWidth = width;
        _cachedHeight = height;
    }
}
