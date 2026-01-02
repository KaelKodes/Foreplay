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
        _aimLine = GetNodeOrNull<MeshInstance3D>("AimLine");
        _landingMarker = GetNodeOrNull<MeshInstance3D>("LandingMarker");

        // Find SwingSystem - Optimized lookup
        if (SwingSystemPath != null && !SwingSystemPath.IsEmpty)
            _swingSystem = GetNodeOrNull<SwingSystem>(SwingSystemPath);

        if (_swingSystem == null)
        {
            // Try common paths and recursive search
            _swingSystem = GetNodeOrNull<SwingSystem>("/root/DrivingRange/SwingSystem");
            if (_swingSystem == null) _swingSystem = GetNodeOrNull<SwingSystem>("/root/TerrainTest/SwingSystem");
            if (_swingSystem == null) _swingSystem = GetTree().Root.FindChild("SwingSystem", true, false) as SwingSystem;
        }

        if (_swingSystem != null)
        {
            GD.Print($"AimAssist: Linked to SwingSystem at {_swingSystem.GetPath()}");
            _swingSystem.ModeChanged -= OnModeChanged;
            _swingSystem.ModeChanged += OnModeChanged;

            _swingSystem.SwingStageChanged -= OnStageChanged;
            _swingSystem.SwingStageChanged += OnStageChanged;
        }
        else
        {
            GD.PrintErr("AimAssist: FAILED to find SwingSystem in scene tree!");
        }

        // Find Camera
        _camera = GetViewport().GetCamera3D();

        // Default to Hidden (Walk Mode)
        Visible = false;
        SetProcess(false);
    }

    private void OnStageChanged(int newStage)
    {
        // 0 = Idle, 1 = Power, 2 = Accuracy, 3 = Executing, 4 = ShotComplete
        if (newStage == (int)SwingStage.Executing)
        {
            _isLocked = true;
        }
        else if (newStage == (int)SwingStage.Idle)
        {
            _isLocked = false;
            UpdateVisuals();
        }
    }

    public override void _Process(double delta)
    {
        if (_swingSystem == null || _camera == null) return;

        if (!_isLocked)
        {
            GlobalPosition = _swingSystem.BallPosition;
            Vector3 camRot = _camera.GlobalRotation;
            GlobalRotation = new Vector3(0, camRot.Y, 0);
            UpdateVisuals();
        }
    }

    private void OnModeChanged(bool isGolfing)
    {
        Visible = isGolfing;
        SetProcess(isGolfing);
        GD.Print($"AimAssist: ModeChanged isGolfing={isGolfing}");

        if (isGolfing)
        {
            _camera = GetViewport().GetCamera3D();
            _isLocked = false;
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        float power = 10.0f;
        if (_swingSystem != null)
        {
            power = _swingSystem.GetEstimatedPower();
        }

        // REVERT TO YESTERDAY'S CALIBRATION (Heuristic: 10 Power = 500y/250m)
        float predictedMeters = power * 25.0f;

        if (Engine.GetFramesDrawn() % 60 == 0)
            GD.Print($"AimAssist: Power={power}, LandingZ={-predictedMeters}");

        if (_aimLine != null)
        {
            _aimLine.Scale = new Vector3(_aimLine.Scale.X, _aimLine.Scale.Y, predictedMeters);
            _aimLine.Position = new Vector3(0, 0, -predictedMeters / 2.0f);
        }

        if (_landingMarker != null)
        {
            _landingMarker.Position = new Vector3(0, 0, -predictedMeters);
        }
    }
}
