using Godot;
using System;

public partial class AimAssist : Node3D
{
    [Export] public NodePath SwingSystemPath;
    private SwingSystem _swingSystem;

    private MeshInstance3D _aimLine;
    private MeshInstance3D _landingMarker;

    private Camera3D _camera;
    private bool _isLocked = false;

    public override void _Ready()
    {
        _aimLine = GetNode<MeshInstance3D>("AimLine");
        _landingMarker = GetNode<MeshInstance3D>("LandingMarker");

        // Find SwingSystem
        if (SwingSystemPath != null)
            _swingSystem = GetNode<SwingSystem>(SwingSystemPath);
        else
            _swingSystem = GetNodeOrNull<SwingSystem>("/root/DrivingRange/SwingSystem");

        if (_swingSystem != null)
        {
            _swingSystem.Connect(SwingSystem.SignalName.ModeChanged, new Callable(this, MethodName.OnModeChanged));
            _swingSystem.Connect(SwingSystem.SignalName.SwingStageChanged, new Callable(this, MethodName.OnStageChanged));
        }

        // Find Camera
        _camera = GetViewport().GetCamera3D();

        // Default to Hidden (Walk Mode)
        Visible = false;
        SetProcess(false);
    }

    private void OnStageChanged(int newStage)
    {
        // Lock the assist visualization once the shot starts executing (ball launches)
        if (newStage == (int)SwingStage.Executing)
        {
            _isLocked = true;
        }
    }

    public override void _Process(double delta)
    {
        if (_swingSystem == null || _camera == null) return;

        if (!_isLocked)
        {
            // Position at Ball
            GlobalPosition = _swingSystem.BallPosition;

            // Match Camera Horizontal Heading
            Vector3 camRot = _camera.GlobalRotation;
            GlobalRotation = new Vector3(0, camRot.Y, 0);

            UpdateVisuals();
        }
    }

    private void OnModeChanged(bool isGolfing)
    {
        Visible = isGolfing;
        SetProcess(isGolfing);

        // Refresh camera ref if needed
        if (isGolfing)
        {
            _camera = GetViewport().GetCamera3D();
            _isLocked = false; // Reset lock when entering Golf Mode
        }

        if (isGolfing) UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        float power = 10.0f; // Default max
        if (_swingSystem != null)
        {
            power = _swingSystem.GetEstimatedPower();
        }

        // Heuristic: 10 Power = 300y. 5 Power = 150y? 
        // Let's assume linear scaling for the "Marker" for now.
        // 300 yards = 274 meters.
        // 274m / 10 = 27.4m per power point.
        float predictedMeters = power * 27.4f;

        if (_aimLine != null)
        {
            _aimLine.Scale = new Vector3(_aimLine.Scale.X, _aimLine.Scale.Y, predictedMeters);
            // Extend along -Z (Forward)
            _aimLine.Position = new Vector3(0, 0, -predictedMeters / 2.0f);
        }

        if (_landingMarker != null)
        {
            _landingMarker.Position = new Vector3(0, 0, -predictedMeters);
        }
    }
}
