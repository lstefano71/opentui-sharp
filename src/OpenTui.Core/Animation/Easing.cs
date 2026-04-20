namespace OpenTui.Core;

/// <summary>
/// Standard easing functions for animation.
/// Matches TypeScript easingFunctions from Timeline.ts.
/// </summary>
public static class Easing
{
    /// <summary>
    /// Performs linear.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of linear.</returns>
    public static float Linear(float t) => t;

    /// <summary>
    /// Performs in quad.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of in quad.</returns>
    public static float InQuad(float t) => t * t;
    /// <summary>
    /// Performs out quad.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of out quad.</returns>
    public static float OutQuad(float t) => t * (2 - t);
    /// <summary>
    /// Performs in out quad.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of in out quad.</returns>
    public static float InOutQuad(float t) =>
        t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;

    /// <summary>
    /// Performs in expo.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of in expo.</returns>
    public static float InExpo(float t) =>
        t == 0 ? 0 : MathF.Pow(2, 10 * (t - 1));
    /// <summary>
    /// Performs out expo.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of out expo.</returns>
    public static float OutExpo(float t) =>
        t >= 1 ? 1 : 1 - MathF.Pow(2, -10 * t);

    /// <summary>
    /// Performs in out sine.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of in out sine.</returns>
    public static float InOutSine(float t) =>
        -(MathF.Cos(MathF.PI * t) - 1) / 2;

    /// <summary>
    /// Performs out bounce.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of out bounce.</returns>
    public static float OutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1 / d1) return n1 * t * t;
        if (t < 2 / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
        if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }

    /// <summary>
    /// Performs in bounce.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of in bounce.</returns>
    public static float InBounce(float t) => 1 - OutBounce(1 - t);

    /// <summary>
    /// Performs out elastic.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of out elastic.</returns>
    public static float OutElastic(float t)
    {
        if (t == 0 || t >= 1) return t;
        float p = 0.3f;
        float s = p / 4;
        return MathF.Pow(2, -10 * t) * MathF.Sin((t - s) * (2 * MathF.PI) / p) + 1;
    }

    /// <summary>
    /// Performs in circ.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of in circ.</returns>
    public static float InCirc(float t) => 1 - MathF.Sqrt(1 - t * t);
    /// <summary>
    /// Performs out circ.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of out circ.</returns>
    public static float OutCirc(float t) { t -= 1; return MathF.Sqrt(1 - t * t); }
    /// <summary>
    /// Performs in out circ.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of in out circ.</returns>
    public static float InOutCirc(float t) =>
        t < 0.5f
            ? (1 - MathF.Sqrt(1 - 4 * t * t)) / 2
            : (MathF.Sqrt(1 - MathF.Pow(-2 * t + 2, 2)) + 1) / 2;

    /// <summary>
    /// Performs in back.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of in back.</returns>
    public static float InBack(float t)
    {
        const float s = 1.70158f;
        return t * t * ((s + 1) * t - s);
    }

    /// <summary>
    /// Performs out back.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of out back.</returns>
    public static float OutBack(float t)
    {
        const float s = 1.70158f;
        t -= 1;
        return t * t * ((s + 1) * t + s) + 1;
    }

    /// <summary>
    /// Performs in out back.
    /// </summary>
    /// <param name="t">The t.</param>
    /// <returns>The result of in out back.</returns>
    public static float InOutBack(float t)
    {
        const float s = 1.70158f * 1.525f;
        if (t < 0.5f)
            return (MathF.Pow(2 * t, 2) * ((s + 1) * 2 * t - s)) / 2;
        t = 2 * t - 2;
        return (t * t * ((s + 1) * t + s) + 2) / 2;
    }

    /// <summary>
    /// Resolves an easing function by name.
    /// </summary>
    public static Func<float, float> ByName(string name) => name switch
    {
        "linear" => Linear,
        "inQuad" => InQuad,
        "outQuad" => OutQuad,
        "inOutQuad" => InOutQuad,
        "inExpo" => InExpo,
        "outExpo" => OutExpo,
        "inOutSine" => InOutSine,
        "outBounce" => OutBounce,
        "inBounce" => InBounce,
        "outElastic" => OutElastic,
        "inCirc" => InCirc,
        "outCirc" => OutCirc,
        "inOutCirc" => InOutCirc,
        "inBack" => InBack,
        "outBack" => OutBack,
        "inOutBack" => InOutBack,
        _ => Linear,
    };
}
