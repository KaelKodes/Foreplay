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
    /// Recalibrated: 50.0m/s is ~112mph Clubhead Speed.
    /// </summary>
    public const float BASE_VELOCITY = 50.0f;

    /// <summary>
    /// Aerodynamic multiplier used in AimAssist to approximate Magnus lift.
    /// 1.95x provides a stronger hang-time for high-spin shots.
    /// </summary>
    public const float LIFT_FACTOR = 1.95f;

    /// <summary>
    /// Standard gravity constant.
    /// </summary>
    public const float GRAVITY = 9.8f;

    // --- AERODYNAMICS (BALL DATA) ---
    public const float DRAG_COEFFICIENT = 0.18f;
    public const float LIFT_COEFFICIENT = 0.15f;
    public const float AIR_DENSITY = 1.225f;
    public const float BALL_AREA = 0.001432f;
    public const float BALL_RADIUS = 0.021f;
    public const float BALL_MASS = 0.045f;

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
