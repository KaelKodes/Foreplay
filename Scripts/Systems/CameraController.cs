using Godot;
using System;

public partial class CameraController : Camera3D
{
    [Export] public NodePath TargetPath;
    [Export] public Vector3 FollowOffset = new Vector3(0, 5, 10);
    [Export] public float SmoothSpeed = 5.0f;

    private Node3D _target;
    private bool _isFollowingBall = false;

    // "Free Look" in this context acts as a "Detach/Debug" toggle.
    // If true, we stop following the target.
    private bool _canFreeLook = false;

    private float _lookSensitivity = 0.3f;

    public override void _Ready()
    {
        SetAsTopLevel(true); // Detach from parent transform to prevent spin
        if (TargetPath != null) _target = GetNode<Node3D>(TargetPath);
    }

    public override void _Input(InputEvent @event)
    {
        // Allow Orbit Rotation (Right Click) regardless of "Free Look" mode
        // unless we strictly want to block it. 
        // For "Walking Mode", we want Orbit.

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

        // If Debug/Free Look is enabled, skip automatic following logic
        if (_canFreeLook) return;

        if (_isFollowingBall)
        {
            // Ball Camera (High Angle Chase) - Logic remains "Chase" style for Ball
            Vector3 targetPos = _target.GlobalPosition + new Vector3(0, 10, -10);
            GlobalPosition = GlobalPosition.Lerp(targetPos, (float)delta * SmoothSpeed);
            LookAt(_target.GlobalPosition, Vector3.Up);
        }
        else
        {
            // Walking Camera (Independent Orbit)
            // Follow Target Position, but respect Camera's OWN Rotation.

            float dist = FollowOffset.Z;
            float height = FollowOffset.Y;

            // Calculate position offset from Camera's current Basis
            // This decouples us from the Player's rotation
            Vector3 desiredOffset = new Vector3(0, height, dist);
            Vector3 orbitPos = _target.GlobalPosition + (GlobalBasis * desiredOffset);

            // Lerp Position for smoothness
            GlobalPosition = GlobalPosition.Lerp(orbitPos, (float)delta * SmoothSpeed);
        }
    }

    public void SetTarget(Node3D newTarget, bool snap = false)
    {
        _target = newTarget;
        if (snap && _target != null)
        {
            // Instantly snap to valid orbit position
            float dist = FollowOffset.Z;
            float height = FollowOffset.Y;
            Vector3 desiredOffset = new Vector3(0, height, dist);
            // Use current rotation basis
            GlobalPosition = _target.GlobalPosition + (GlobalBasis * desiredOffset);
        }
    }

    public void SetFollowing(bool following)
    {
        _isFollowingBall = following;
        if (_isFollowingBall) _canFreeLook = false; // Ensure we move!
    }

    public void SetFreeLook(bool enabled)
    {
        _canFreeLook = enabled;
    }

    public void Initialize(Vector3 pos, Vector3 rotDegrees) { } // Stub
    public void ToggleMode(int mode) { } // Stub
}
