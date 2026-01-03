namespace Golf;

public static class GolfConstants
{
    // --- HUD & VISUAL SCALING ---
    /// <summary>
    /// The ratio of world meters to reported yards in the HUD.
    /// 1.0 meter in the world = 2.0 "yards" in the labels.
    /// </summary>
    public const float UNIT_RATIO = 2.0f;

    // --- PHYSICS BASELINES ---
    /// <summary>
    /// The base meters-per-second for a 1.0x power Driver.
    /// Combined with UNIT_RATIO and LIFT_FACTOR, this determines carry.
    /// </summary>
    public const float BASE_VELOCITY = 46.0f;

    /// <summary>
    /// Aerodynamic multiplier used in AimAssist to approximate Magnus lift.
    /// 1.85x means the ball hangs for ~85% longer than a pure parabola.
    /// </summary>
    public const float LIFT_FACTOR = 1.85f;

    /// <summary>
    /// Standard gravity constant.
    /// </summary>
    public const float GRAVITY = 9.8f;

    // --- SWING SYSTEM ---
    /// <summary>
    /// The power bar value that represents a "Full Speed" shot (100% of base velocity).
    /// </summary>
    public const float PEAK_POWER_VALUE = 94.0f;

    /// <summary>
    /// The accuracy bar value that represents a "Perfect" strike (center).
    /// </summary>
    public const float PERFECT_ACCURACY_VALUE = 25.0f;
}
