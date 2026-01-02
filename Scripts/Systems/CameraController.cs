using Godot;
using System;

public partial class CameraController : Camera3D
{
    [Export] public NodePath TargetPath;
    [Export] public Vector3 FollowOffset = new Vector3(0, 5, 10);
    [Export] public float SmoothSpeed = 5.0f;

    private Node3D _target;
    private Vector3 _originalPosition;
    private Vector3 _originalRotation;
    private bool _isFollowing = false;

    public override void _Ready()
    {
        if (TargetPath != null)
            _target = GetNode<Node3D>(TargetPath);

        _originalPosition = Position;
        _originalRotation = GlobalRotation; // Use Global to account for parent rotation
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isFollowing && _target != null)
        {
            // Calculate Offset using GLOBAL rotation to ensure we are actually behind the ball
            // regardless of parent rotation (Player/Tee).
            Transform3D originTransform = new Transform3D(Basis.FromEuler(_originalRotation), Vector3.Zero);
            Vector3 relativeOffset = originTransform.Basis * FollowOffset;

            Vector3 targetPos = _target.GlobalPosition + relativeOffset;
            GlobalPosition = GlobalPosition.Lerp(targetPos, (float)delta * SmoothSpeed);
        }
    }

    public void SetFollowing(bool following)
    {
        _isFollowing = following;
        if (!following)
        {
            // Reset to tee view
            Position = _originalPosition;
            GlobalRotation = _originalRotation;
        }
    }
}
