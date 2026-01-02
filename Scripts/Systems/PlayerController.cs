using Godot;
using System;

public partial class PlayerController : Node3D
{
    [Export] public float RotationSpeed = 1.0f;
    [Export] public NodePath CameraPath;
    private CameraController _camera;

    public override void _Ready()
    {
        if (CameraPath != null) _camera = GetNode<CameraController>(CameraPath);

        // Force initial orientation facing down-range (+Z)
        // With CameraPath fixed, 180 should correctly face the fairway.
        RotationDegrees = new Vector3(0, 180, 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        // Only allow rotation if camera is present (always Tee mode now)
        if (_camera == null) return;

        float rot = 0.0f;
        if (Input.IsKeyPressed(Key.A) || Input.IsActionPressed("ui_left")) rot += 1.0f;
        if (Input.IsKeyPressed(Key.D) || Input.IsActionPressed("ui_right")) rot -= 1.0f;

        if (rot != 0.0f)
        {
            RotateY(rot * RotationSpeed * (float)delta);
            // GD.Print($"Player Rotating: {RotationDegrees.Y}"); 
        }
    }

    public void TeleportTo(Vector3 position, Vector3 lookAtTarget)
    {
        GlobalPosition = position;

        // Face the target
        // LookAt points -Z towards target, which matches our Forward convention
        LookAt(new Vector3(lookAtTarget.X, GlobalPosition.Y, lookAtTarget.Z), Vector3.Up);

        // Ensure rotation is clean (Y-axis only)
        RotationDegrees = new Vector3(0, RotationDegrees.Y, 0);
    }
}
