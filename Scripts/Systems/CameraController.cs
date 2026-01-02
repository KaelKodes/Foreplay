using Godot;
using System;

public partial class CameraController : Camera3D
{
    [Export] public NodePath TargetPath;
    [Export] public Vector3 FollowOffset = new Vector3(0, 5, 10);
    [Export] public float SmoothSpeed = 5.0f;

    private Node3D _target;
    private bool _isFollowingBall = false;

    public override void _Ready()
    {
        if (TargetPath != null) _target = GetNode<Node3D>(TargetPath);
    }

    public void Initialize(Vector3 pos, Vector3 rotDegrees)
    {
        // No-op for now, simplified
    }

    private bool _canFreeLook = false;
    private float _lookSensitivity = 0.3f;

    public override void _Input(InputEvent @event)
    {
        if (!_canFreeLook) return;

        if (@event is InputEventMouseMotion motion && Input.IsMouseButtonPressed(MouseButton.Right))
        {
            // Rotate camera based on mouse motion
            RotationDegrees -= new Vector3(motion.Relative.Y, motion.Relative.X, 0) * _lookSensitivity;

            // Clamp pitch to prevent flipping
            Vector3 rot = RotationDegrees;
            rot.X = Mathf.Clamp(rot.X, -80, 80);
            RotationDegrees = rot;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_target == null) return;

        // If Free Look is enabled, skip automatic following/positioning logic
        // This allows the player to rotate the camera freely from its last position.
        if (_canFreeLook) return;

        if (_isFollowingBall)
        {
            // Follow Ball Logic
            // Ball moves along World +Z. Camera should be World -Z relative to ball to look forward.
            // Offset (0, 10, -10) places camera behind and above.
            Vector3 targetPos = _target.GlobalPosition + new Vector3(0, 10, -10);
            GlobalPosition = GlobalPosition.Lerp(targetPos, (float)delta * SmoothSpeed);
            LookAt(_target.GlobalPosition);
        }
        else
        {
            // Tee View (Attached to PlayerPlaceholder)
            // Maintain local offset and consistent downward tilt
            Position = Position.Lerp(FollowOffset, (float)delta * SmoothSpeed);
            RotationDegrees = RotationDegrees.Lerp(new Vector3(-20, 0, 0), (float)delta * SmoothSpeed);
        }
    }

    public void SetFollowing(bool following)
    {
        _isFollowingBall = following;
    }

    public void SetFreeLook(bool enabled)
    {
        _canFreeLook = enabled;
    }

    public void ToggleMode(int mode) { } // Stub for compatibility
}
