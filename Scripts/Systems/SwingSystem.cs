using Godot;
using System;

public enum SwingStage
{
    Idle,
    Power,
    Accuracy,
    Executing,
    ShotComplete
}

public partial class SwingSystem : Node
{
    [Export] public float SwingSpeed = 1.0f;
    [Export] public NodePath BallPath;
    [Export] public NodePath CameraPath;
    [Export] public NodePath WindSystemPath;

    private BallController _ball;
    private CameraController _camera;
    private WindSystem _windSystem;
    private BallLieSystem _lieSystem;
    private SwingStage _stage = SwingStage.Idle;

    private float _powerValue = 0.0f;
    private float _accuracyValue = 0.0f;
    private Vector2 _spinIntent = Vector2.Zero;
    private float _powerOverride = -1.0f; // Debug override for driving range
    private float _timer = 0.0f;
    private bool _isBotched = false;
    private double _lastInputTime = 0;

    private Stats _playerStats = new Stats();

    [Signal] public delegate void SwingStageChangedEventHandler(int newStage);
    [Signal] public delegate void SwingValuesUpdatedEventHandler(float currentBarValue, float lockedPower, float lockedAccuracy);
    [Signal] public delegate void ShotDistanceUpdatedEventHandler(float carry, float total);
    [Signal] public delegate void ShotResultEventHandler(float power, float accuracy);

    // New State Variables
    private bool _swinging = false;
    private bool _isReturnPhase = false;
    private float _lockedPower = -1.0f;
    private float _lockedAccuracy = -1.0f;

    public override void _Ready()
    {
        if (BallPath != null)
        {
            _ball = GetNode<BallController>(BallPath);
            _ball.Connect(BallController.SignalName.BallSettled, new Callable(this, MethodName.OnBallSettled));
            _ball.Connect(BallController.SignalName.BallCarried, new Callable(this, MethodName.OnBallCarried));
        }

        if (CameraPath != null) _camera = GetNode<CameraController>(CameraPath);

        GD.Print("SwingSystem _Ready: Checking WindSystem...");
        if (WindSystemPath != null)
        {
            GD.Print($"SwingSystem _Ready: WindSystemPath is {WindSystemPath}");
            _windSystem = GetNode<WindSystem>(WindSystemPath);
            if (_windSystem != null) GD.Print("SwingSystem _Ready: WindSystem node found!");
            else GD.Print("SwingSystem _Ready: GetNode returned NULL for WindSystem!");
        }
        else
        {
            GD.Print("SwingSystem _Ready: WindSystemPath is NULL!");
        }

        _lieSystem = new BallLieSystem();
        AddChild(_lieSystem);

        LoadStats();
    }

    private void OnBallCarried(float carry)
    {
        EmitSignal(SignalName.ShotDistanceUpdated, carry, -1);
    }

    private void OnBallSettled(float total)
    {
        EmitSignal(SignalName.ShotDistanceUpdated, -1, total);
        _stage = SwingStage.ShotComplete; // Block input until Next Shot
        if (_camera != null) _camera.SetFreeLook(true);
    }

    public override void _Process(double delta)
    {
        // Debug speed sent to HUD during movement
        // Debug speed sent to HUD during movement
        if ((_stage == SwingStage.Executing || _stage == SwingStage.Idle || _stage == SwingStage.ShotComplete) && _ball != null && _ball.LinearVelocity.Length() > 0.1f)
        {
            EmitSignal(SignalName.ShotDistanceUpdated, -2, _ball.LinearVelocity.Length());
        }

        if (_stage == SwingStage.Power)
        {
            // 0 -> 100 -> 0 Loop
            float speed = SwingSpeed * 100.0f;

            if (!_isReturnPhase)
            {
                _timer += (float)delta * speed;
                if (_timer >= 100.0f)
                {
                    _timer = 100.0f;
                    _isReturnPhase = true;
                }
            }
            else
            {
                _timer -= (float)delta * speed; // Same speed back down

                // RESET Condition: Below 50 on return without Power Lock
                if (_timer < 50.0f && _lockedPower < 0)
                {
                    ResetSwing();
                    return;
                }

                // AUTO-FIRE Condition: Reaches 0 with Power Lock (Missed Accuracy)
                if (_timer <= 0.0f)
                {
                    if (_lockedPower > 0)
                    {
                        _lockedAccuracy = 0.0f; // Worst possible accuracy (Fade)
                        ExecuteShot();
                    }
                    else
                    {
                        ResetSwing();
                    }
                    return;
                }
            }

            EmitSignal(SignalName.SwingValuesUpdated, _timer, _lockedPower, _lockedAccuracy);

        }
    }

    public void HandleInput()
    {
        double currentTime = Time.GetTicksMsec();
        if (currentTime - _lastInputTime < 250.0) return; // 250ms debounce

        switch (_stage)
        {
            case SwingStage.Idle:
                StartSwing();
                break;
            case SwingStage.Power: // Active Cycle
                ProcessCycleInput();
                break;
            case SwingStage.ShotComplete:
                // DO NOTHING. Input is blocked.
                break;
        }
    }

    private void StartSwing()
    {
        _lastInputTime = Time.GetTicksMsec();
        _stage = SwingStage.Power; // Using 'Power' as the generic 'Cycling' state
        _timer = 0.0f;
        _isReturnPhase = false;
        _lockedPower = -1.0f;
        _lockedAccuracy = -1.0f;
        _isBotched = false;
        EmitSignal(SignalName.SwingStageChanged, (int)_stage);
    }

    private void ProcessCycleInput()
    {
        _lastInputTime = Time.GetTicksMsec();

        // 1. Lock Power (Anytime before Accuracy zone)
        if (_lockedPower < 0)
        {
            _lockedPower = _timer;
            // Don't stop animation! Just lock value.
            return;
        }

        // 2. Lock Accuracy (Only in Accuracy Zone < 50 on return)
        if (_lockedPower > 0 && _isReturnPhase && _timer <= 50.0f)
        {
            _lockedAccuracy = _timer;
            StopAccuracy();
        }
    }


    private void StopAccuracy()
    {
        _stage = SwingStage.Executing;
        EmitSignal(SignalName.SwingStageChanged, (int)_stage);

        // Force final update to UI so the Locked Accuracy marker appears
        EmitSignal(SignalName.SwingValuesUpdated, _timer, _lockedPower, _lockedAccuracy);

        ExecuteShot();
    }

    private void ExecuteShot()
    {
        // Use Locked Values
        float powerVal = _lockedPower;
        float accuracyVal = (_lockedAccuracy >= 0) ? _lockedAccuracy : 0.0f;

        EmitSignal(SignalName.ShotResult, powerVal, accuracyVal);
        BallLie lie = _lieSystem.GetCurrentLie(_ball.GlobalPosition);

        Vector3 direction = -GetViewport().GetCamera3D().GlobalTransform.Basis.Z;
        direction.Y = 0.23f + lie.LaunchAngleBonus; // Apply launch angle bonus
        direction = direction.Normalized();

        float powerToUse = (_powerOverride > 0) ? _powerOverride : _playerStats.Power;
        float powerStatMult = powerToUse / 10.0f;

        // CALIBRATION:
        // Baseline: 90 Power = 1.0x (Target 300y)
        // Max: 100 Power = ~1.06x (Target 320y)
        float baseVelocity = 82.0f; // Boosted to compensate for 0.18 drag
        float normalizedPower = powerVal / 90.0f;

        // Botch disabled per request, relying on 0-accuracy penalty
        float overswingMult = 1.0f;

        float launchPower = normalizedPower * baseVelocity * powerStatMult * lie.PowerEfficiency * overswingMult;

        // Spin Shaping
        // 25 = Center. 
        // >25 (26-50) = Pos Error = DRAW (Left)
        // <25 (0-24) = Neg Error = FADE (Right)
        float accuracyError = accuracyVal - 25.0f;

        float controlMult = 1.0f / (_playerStats.Control / 10.0f);

        // Shaping spin: +Value = Left Curve (Draw), -Value = Right Curve (Fade)
        // AMPLIFIED 7x: 25 error * 35.0 = 875 rad/s (Pro-level shaping)
        float shapingSpin = accuracyError * 35.0f * controlMult;
        if (!_playerStats.IsRightHanded) shapingSpin *= -1;

        // Launch Angle (Push/Pull Effect):
        // AMPLIFIED 7x: 25 error * 0.056 = 1.4 rad (~80 degrees max off-line)
        float pushPullMultiplier = 0.056f;
        float timingOffset = -accuracyError * pushPullMultiplier;

        Vector3 velocity = direction * launchPower;
        velocity = velocity.Rotated(Vector3.Up, timingOffset);

        // Backspin
        float touchMult = _playerStats.Touch / 10.0f;
        float baselineBackspin = 310.0f * (normalizedPower * powerStatMult) * lie.SpinModifier;

        float totalBackspin = baselineBackspin + (_spinIntent.Y * 60.0f * touchMult);
        float totalSidespin = (shapingSpin + (_spinIntent.X * 40.0f * touchMult)) * lie.SpinModifier;

        // SPIN ALIGNMENT FIX:
        // We need the backspin axis to be the "Right" vector relative to launch direction.
        Vector3 launchDirHorizontal = new Vector3(velocity.X, 0, velocity.Z).Normalized();
        Vector3 rightDir = launchDirHorizontal.Cross(Vector3.Up).Normalized();

        // Final Spin = (RightDir * Backspin) + (UpDir * Sidespin)
        Vector3 spin = (rightDir * totalBackspin) + (Vector3.Up * totalSidespin);

        GD.Print($"LAUNCH: Power={powerVal:F1} (Stat={_playerStats.Power}) Acc={accuracyVal:F1} Err={accuracyError:F1} | Spin={spin} Vel={velocity.Length():F1} Offset={timingOffset:F3}");

        // Launch!
        if (_windSystem != null)
        {
            _ball.SetWind(_windSystem.GetWindVelocityVector());
        }

        _ball.Launch(velocity, spin);
        if (_camera != null) _camera.SetFollowing(true);
        UpdateAnger(accuracyError); // Pass error, not raw value
    }

    private void UpdateAnger(float accuracyError)
    {
        float error = Math.Abs(accuracyError);
        // Tolerance: +/- 5 units from 25
        if (error > 5.0f)
        {
            _playerStats.Anger += 1.0f * (error - 5.0f);
        }
        else
        {
            _playerStats.Anger -= 5.0f;
        }
        _playerStats.Anger = Mathf.Clamp(_playerStats.Anger, 0, 100);
        SavePlayerProgress();
    }

    private void SavePlayerProgress()
    {
        try
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE PlayerStats SET Anger = @anger WHERE Id = 1";
                command.Parameters.AddWithValue("@anger", _playerStats.Anger);
                command.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to save progress: {e.Message}");
        }
    }

    private void LoadStats()
    {
        try
        {
            using (var connection = DatabaseManager.GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Power, Control, Touch, IsRightHanded, Anger FROM PlayerStats WHERE Id = 1";
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        _playerStats.Power = reader.GetInt32(0);
                        _playerStats.Control = reader.GetInt32(1);
                        _playerStats.Touch = reader.GetInt32(2);
                        _playerStats.IsRightHanded = reader.GetInt32(3) == 1;
                        _playerStats.Anger = reader.IsDBNull(4) ? 0 : (float)reader.GetDouble(4);
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to load stats: {e.Message}");
        }
    }

    [Signal] public delegate void StrokeUpdatedEventHandler(int stroke);

    // ...
    private int _strokeCount = 1;

    public void PrepareNextShot()
    {
        _strokeCount++;
        EmitSignal(SignalName.StrokeUpdated, _strokeCount);

        // 1. Move Player to Ball
        Node3D player = GetNode<Node3D>("../PlayerPlaceholder");
        if (player != null && player is PlayerController pc)
        {
            // Offset player slightly behind ball? Or right on top?
            // Usually behind. Let's say 0.5m back along the line to the hole.
            // For now, just ON the ball acts as the pivot.
            // Actually, we need to find the Target Green to look at it.
            // Hardcoded 600y Green pos for now or find it dynamically?
            // Let's look at World +Z (Fairway) default if no target.
            Vector3 lookTarget = new Vector3(0, 0, 1000);
            pc.TeleportTo(_ball.GlobalPosition, lookTarget);
        }

        // 2. Reset Ball Physics (Freeze in place)
        _ball.PrepareNextShot();

        // 3. Reset System State
        _stage = SwingStage.Idle;
        _timer = 0.0f;
        _isReturnPhase = false;
        _lockedPower = -1.0f;
        _lockedAccuracy = -1.0f;

        if (_camera != null)
        {
            _camera.SetFollowing(false);
            _camera.SetFreeLook(false);
        }
        EmitSignal(SignalName.SwingStageChanged, (int)_stage);
        EmitSignal(SignalName.SwingValuesUpdated, 0, -1, -1);
        EmitSignal(SignalName.ShotDistanceUpdated, 0, 0);
    }

    public void ResetSwing()
    {
        // Full Reset (Back to Tee)
        _strokeCount = 1;
        EmitSignal(SignalName.StrokeUpdated, _strokeCount);

        _stage = SwingStage.Idle;
        _timer = 0.0f;
        _isReturnPhase = false;
        _lockedPower = -1.0f;
        _lockedAccuracy = -1.0f;

        _ball.Reset();

        // Reset Player Config
        Node3D player = GetNode<Node3D>("../PlayerPlaceholder");
        if (player is PlayerController pc)
        {
            pc.GlobalPosition = new Vector3(0, 0.1f, 0);
            pc.RotationDegrees = new Vector3(0, 180, 0);
        }

        if (_camera != null)
        {
            _camera.SetFollowing(false);
            _camera.SetFreeLook(false);
        }
        EmitSignal(SignalName.SwingStageChanged, (int)_stage);
        EmitSignal(SignalName.SwingValuesUpdated, 0, -1, -1);
        EmitSignal(SignalName.ShotDistanceUpdated, 0, 0);
    }

    public void SetSpinIntent(Vector2 intent)
    {
        if (_stage == SwingStage.Idle)
        {
            _spinIntent = intent;
        }
    }

    public void SetPowerOverride(float power)
    {
        _powerOverride = power;
        GD.Print($"DEBUG POWER OVERRIDE SET: {_powerOverride}");
    }

    public float GetAnger() => _playerStats.Anger;

    public float GetEstimatedPower()
    {
        return (_powerOverride > 0) ? _powerOverride : _playerStats.Power;
    }
}
