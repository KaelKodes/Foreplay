using Godot;
using System;

public partial class WindSystem : Node
{
    [Signal] public delegate void WindChangedEventHandler(Vector3 direction, float speedMph);

    // Config
    [Export] public float MaxWindSpeedMph = 20.0f;
    [Export] public float MinWindSpeedMph = 0.0f;

    // State
    public Vector3 WindDirection { get; private set; } = Vector3.Forward;
    public float WindSpeedMph { get; private set; } = 0.0f;
    public bool IsWindEnabled { get; set; } = true;

    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    public override void _Ready()
    {
        _rng.Randomize();
        RandomizeWind();
    }

    public void SetWindDirection(Vector3 direction)
    {
        WindDirection = direction.Normalized();
        EmitSignal(SignalName.WindChanged, WindDirection, WindSpeedMph);
        GD.Print($"WIND DIRECTION SET: {WindDirection}");
    }

    public void SetWindSpeed(float speed)
    {
        WindSpeedMph = Mathf.Clamp(speed, 0, 30.0f); // Allow up to 30mph for manual testing
        EmitSignal(SignalName.WindChanged, WindDirection, WindSpeedMph);
        GD.Print($"WIND SPEED SET: {WindSpeedMph} mph");
    }

    public void ToggleWind()
    {
        IsWindEnabled = !IsWindEnabled;
        GD.Print($"WIND TOGGLED: {(IsWindEnabled ? "ON" : "OFF")}");
    }

    public void RandomizeWind()
    {
        // Random Speed
        WindSpeedMph = 20.0f; // Kept at 20mph for current calibration phase

        // Random Direction (0-360 degrees on Y axis)
        float angleY = _rng.RandfRange(0, Mathf.Tau);
        WindDirection = new Vector3(Mathf.Sin(angleY), 0, Mathf.Cos(angleY)).Normalized();

        EmitSignal(SignalName.WindChanged, WindDirection, WindSpeedMph);

        GD.Print($"WIND: {WindSpeedMph:F1} mph, Dir: {WindDirection}");
    }

    public Vector3 GetWindVelocityVector()
    {
        if (!IsWindEnabled) return Vector3.Zero;

        // Convert MPH to m/s? 
        // 1 mph = 0.44704 m/s.
        // Game physics is roughly real-world metric.
        float speedMs = WindSpeedMph * 0.44704f;
        return WindDirection * speedMs;
    }
}
