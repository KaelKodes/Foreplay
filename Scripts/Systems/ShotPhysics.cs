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

        // 1. Loft and AoA Calculation
        float staticLoft = p.SelectedClub != null ? p.SelectedClub.LoftDegrees : 15.0f;

        // Apply default AoA based on club type if offset is near zero
        float effectiveAoA = p.AoAOffset;
        if (p.SelectedClub != null && Mathf.Abs(p.AoAOffset) < 0.01f) // No user manual tilt
        {
            if (p.SelectedClub.Type == ClubType.Driver)
                effectiveAoA = 3.5f; // Standard Driver is hit "Up"
            else if (p.SelectedClub.Type == ClubType.Iron || p.SelectedClub.Type == ClubType.Wedge)
                effectiveAoA = -2.5f; // Irons hit "Down"
        }

        float totalLoft = staticLoft + effectiveAoA;

        // Convert degrees to the direction.Y ratio (approx)
        float loftRad = Mathf.DegToRad(totalLoft);
        direction.Y = Mathf.Sin(loftRad) + p.CurrentLie.LaunchAngleBonus;
        direction = direction.Normalized();

        // 2. Power and Velocity (Smash Factor)
        float powerToUse = (p.PowerOverride > 0) ? p.PowerOverride : p.PlayerStats.Power;
        float powerStatMult = powerToUse / 10.0f;

        float baseVelocity = Golf.GolfConstants.BASE_VELOCITY;
        // smashFactor = energy transfer efficiency
        float smashFactor = p.SelectedClub != null ? p.SelectedClub.PowerMultiplier : 1.0f;
        // headSpeedMult = clubhead speed (based on club length)
        float headSpeedMult = p.SelectedClub != null ? p.SelectedClub.HeadSpeedMultiplier : 1.0f;

        float normalizedPower = p.PowerValue / Golf.GolfConstants.PEAK_POWER_VALUE;
        float launchPower = normalizedPower * baseVelocity * powerStatMult * headSpeedMult * smashFactor * p.CurrentLie.PowerEfficiency;

        // 3. Accuracy and Side Spin
        float accuracyError = p.AccuracyValue - Golf.GolfConstants.PERFECT_ACCURACY_VALUE;
        float forgiveness = p.SelectedClub != null ? p.SelectedClub.SweetSpotSize : 1.0f;
        accuracyError /= forgiveness;

        if (p.PowerValue > Golf.GolfConstants.PEAK_POWER_VALUE)
        {
            float overpowerFactor = 1.0f + (p.PowerValue - Golf.GolfConstants.PEAK_POWER_VALUE) * 0.15f;
            accuracyError *= (overpowerFactor / forgiveness);
            launchPower *= (1.0f + (p.PowerValue - Golf.GolfConstants.PEAK_POWER_VALUE) * 0.01f);
        }

        float controlMult = 1.0f / (p.PlayerStats.Control / 10.0f);
        float shapingSpin = (accuracyError * 45.0f * controlMult) / forgiveness; // Boosted side spin sensitivity
        if (!p.IsRightHanded) shapingSpin *= -1;

        float timingOffset = -accuracyError * 0.056f;

        Vector3 velocity = direction * launchPower;
        velocity = velocity.Rotated(Vector3.Up, timingOffset);

        // 4. Backspin Calculation
        float touchMult = p.PlayerStats.Touch / 10.0f;
        float clubSpinMult = p.SelectedClub != null ? p.SelectedClub.SpinMultiplier : 1.0f;

        // Dynamic Loft increases spin (hitting down/more loft = more spin)
        // Recalibrated: less aggressive bonus to prevent "orbiting" irons
        float loftSpinBonus = 1.0f + (totalLoft / 45.0f);

        float baselineBackspin = 380.0f * (normalizedPower * powerStatMult) * p.CurrentLie.SpinModifier * clubSpinMult * loftSpinBonus;
        float totalBackspin = baselineBackspin + (p.SpinIntent.Y * 80.0f * touchMult);
        float totalSidespin = (shapingSpin + (p.SpinIntent.X * 50.0f * touchMult)) * p.CurrentLie.SpinModifier;

        Vector3 launchDirHorizontal = new Vector3(velocity.X, 0, velocity.Z).Normalized();
        Vector3 rightDir = launchDirHorizontal.Cross(Vector3.Up).Normalized();

        Vector3 spin = (rightDir * totalBackspin) + (Vector3.Up * totalSidespin);

        return new ShotResult { Velocity = velocity, Spin = spin };
    }
}
