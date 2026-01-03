using Godot;

public static class ShotPhysics
{
    public struct ShotParams
    {
        public float PowerValue;
        public float AccuracyValue;
        public Vector2 SpinIntent;
        public float PowerOverride;
        public Stats PlayerStats;
        public BallLie CurrentLie;
        public Vector3 CameraCameraForward;
        public bool IsRightHanded;
        public GolfClub SelectedClub; // Added
        public float AoAOffset;       // Added (Angle of Attack)
    }

    public struct ShotResult
    {
        public Vector3 Velocity;
        public Vector3 Spin;
    }

    public static ShotResult CalculateShot(ShotParams p)
    {
        Vector3 direction = p.CameraCameraForward;

        // Use Club Loft + AoA Offset
        float totalLoft = p.SelectedClub != null ? p.SelectedClub.LoftDegrees : 15.0f;
        totalLoft += p.AoAOffset;

        // Convert degrees to the direction.Y ratio (approx)
        // Since Y is normalized, Sin(Loft) is the Y component.
        float loftRad = Mathf.DegToRad(totalLoft);
        direction.Y = Mathf.Sin(loftRad) + p.CurrentLie.LaunchAngleBonus;
        direction = direction.Normalized();

        float powerToUse = (p.PowerOverride > 0) ? p.PowerOverride : p.PlayerStats.Power;
        float powerStatMult = powerToUse / 10.0f;

        float baseVelocity = Golf.GolfConstants.BASE_VELOCITY;
        float clubPowerMult = p.SelectedClub != null ? p.SelectedClub.PowerMultiplier : 1.0f;

        float normalizedPower = p.PowerValue / Golf.GolfConstants.PEAK_POWER_VALUE;
        float launchPower = normalizedPower * baseVelocity * powerStatMult * clubPowerMult * p.CurrentLie.PowerEfficiency;

        float accuracyError = p.AccuracyValue - Golf.GolfConstants.PERFECT_ACCURACY_VALUE;

        // Apply Club Forgiveness (MOI)
        float forgiveness = p.SelectedClub != null ? p.SelectedClub.SweetSpotSize : 1.0f;
        accuracyError /= forgiveness;

        // Overpower Penalty logic
        if (p.PowerValue > Golf.GolfConstants.PEAK_POWER_VALUE)
        {
            float overpowerFactor = 1.0f + (p.PowerValue - Golf.GolfConstants.PEAK_POWER_VALUE) * 0.15f; // Exponential-ish multiplier for errors
            accuracyError *= (overpowerFactor / forgiveness); // Offset slightly by forgiveness
            // Also slightly boost launch power but with high variance/risk
            launchPower *= (1.0f + (p.PowerValue - Golf.GolfConstants.PEAK_POWER_VALUE) * 0.01f);
        }

        float controlMult = 1.0f / (p.PlayerStats.Control / 10.0f);
        float shapingSpin = (accuracyError * 35.0f * controlMult) / forgiveness;
        if (!p.IsRightHanded) shapingSpin *= -1;

        float timingOffset = -accuracyError * 0.056f;

        Vector3 velocity = direction * launchPower;
        velocity = velocity.Rotated(Vector3.Up, timingOffset);

        float touchMult = p.PlayerStats.Touch / 10.0f;
        float clubSpinMult = p.SelectedClub != null ? p.SelectedClub.SpinMultiplier : 1.0f;

        float baselineBackspin = 310.0f * (normalizedPower * powerStatMult) * p.CurrentLie.SpinModifier * clubSpinMult;
        float totalBackspin = baselineBackspin + (p.SpinIntent.Y * 60.0f * touchMult);
        float totalSidespin = (shapingSpin + (p.SpinIntent.X * 40.0f * touchMult)) * p.CurrentLie.SpinModifier;

        Vector3 launchDirHorizontal = new Vector3(velocity.X, 0, velocity.Z).Normalized();
        Vector3 rightDir = launchDirHorizontal.Cross(Vector3.Up).Normalized();

        Vector3 spin = (rightDir * totalBackspin) + (Vector3.Up * totalSidespin);

        return new ShotResult { Velocity = velocity, Spin = spin };
    }
}
