using Godot;
using System;

public partial class AimAssist : Node3D
{
    [Export] public NodePath SwingSystemPath;
    private SwingSystem _swingSystem;

    private MeshInstance3D _aimLine;
    private MeshInstance3D _landingMarker;

    public override void _Ready()
    {
        _aimLine = GetNode<MeshInstance3D>("AimLine");
        _landingMarker = GetNode<MeshInstance3D>("LandingMarker");

        // Find SwingSystem if not assigned (it's a sibling of PlayerPlaceholder's parent usually, or we search root)
        if (SwingSystemPath != null)
            _swingSystem = GetNode<SwingSystem>(SwingSystemPath);
        else
            _swingSystem = GetNodeOrNull<SwingSystem>("/root/DrivingRange/SwingSystem");

        UpdateVisuals();
    }

    public override void _Process(double delta)
    {
        UpdateVisuals();
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
