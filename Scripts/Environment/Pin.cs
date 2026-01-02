using Godot;
using System;

public partial class Pin : Node3D
{
    [Export] public NodePath WindSystemPath;
    private WindSystem _windSystem;
    private Node3D _flag;

    public override void _Ready()
    {
        _flag = GetNode<Node3D>("Flag");

        // Try to find WindSystem if path not set
        if (WindSystemPath != null)
        {
            _windSystem = GetNode<WindSystem>(WindSystemPath);
        }
        else
        {
            // Search up the tree or in the main internal structure
            // NOTE: Since Pin is inside TargetGreen which is in DrivingRange, 
            // we might need to search relative to root or use a singleton approach later.
            // For now, let's try looking at the root level if not assigned.
            _windSystem = GetNodeOrNull<WindSystem>("/root/DrivingRange/WindSystem");
        }

        if (_windSystem != null)
        {
            _windSystem.WindChanged += OnWindChanged;
            // set initial rotation
            OnWindChanged(_windSystem.WindDirection, _windSystem.WindSpeedMph);
        }
    }

    private void OnWindChanged(Vector3 direction, float speed)
    {
        if (_flag == null) return;

        // Rotate Flag to face the wind direction
        // Mathf.Atan2(x, z) gives angle from Z axis
        float targetRotation = Mathf.Atan2(direction.X, direction.Z);

        // Use a tween for smooth rotation if desired, but direct set is fine for now
        _flag.Rotation = new Vector3(0, targetRotation, 0);
    }
}
