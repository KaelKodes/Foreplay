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
    }

    public struct ShotResult
    {
        public Vector3 Velocity;
        public Vector3 Spin;
    }

    public static ShotResult CalculateShot(ShotParams p)
    {
        Vector3 direction = p.CameraCameraForward;
        direction.Y = 0.23f + p.CurrentLie.LaunchAngleBonus;
        direction = direction.Normalized();

        float powerToUse = (p.PowerOverride > 0) ? p.PowerOverride : p.PlayerStats.Power;
        float powerStatMult = powerToUse / 10.0f;
        float baseVelocity = 82.0f;
        float normalizedPower = p.PowerValue / 94.0f;
        float launchPower = normalizedPower * baseVelocity * powerStatMult * p.CurrentLie.PowerEfficiency;

        float accuracyError = p.AccuracyValue - 25.0f;

        // Overpower Penalty logic
        if (p.PowerValue > 94.0f)
        {
            float overpowerFactor = 1.0f + (p.PowerValue - 94.0f) * 0.15f; // Exponential-ish multiplier for errors
            accuracyError *= overpowerFactor;
            // Also slightly boost launch power but with high variance/risk
            launchPower *= (1.0f + (p.PowerValue - 94.0f) * 0.01f);
        }

        float controlMult = 1.0f / (p.PlayerStats.Control / 10.0f);
        float shapingSpin = accuracyError * 35.0f * controlMult;
        if (!p.IsRightHanded) shapingSpin *= -1;

        float timingOffset = -accuracyError * 0.056f;

        Vector3 velocity = direction * launchPower;
        velocity = velocity.Rotated(Vector3.Up, timingOffset);

        float touchMult = p.PlayerStats.Touch / 10.0f;
        float baselineBackspin = 310.0f * (normalizedPower * powerStatMult) * p.CurrentLie.SpinModifier;
        float totalBackspin = baselineBackspin + (p.SpinIntent.Y * 60.0f * touchMult);
        float totalSidespin = (shapingSpin + (p.SpinIntent.X * 40.0f * touchMult)) * p.CurrentLie.SpinModifier;

        Vector3 launchDirHorizontal = new Vector3(velocity.X, 0, velocity.Z).Normalized();
        Vector3 rightDir = launchDirHorizontal.Cross(Vector3.Up).Normalized();

        Vector3 spin = (rightDir * totalBackspin) + (Vector3.Up * totalSidespin);

        return new ShotResult { Velocity = velocity, Spin = spin };
    }
}
