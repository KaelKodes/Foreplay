using Godot;
using System;

public partial class TeeBox : InteractableObject
{
    private bool _isMoving = false;
    private Camera3D _camera;
    private SwingSystem _swingSystem;
    private HeightmapTerrain _terrain; // Use global search or passed ref

    public override void _Ready()
    {
        base._Ready();
        _swingSystem = GetNodeOrNull<SwingSystem>("/root/FoxHollowHole1/SwingSystem")
                       ?? GetNodeOrNull<SwingSystem>("/root/DrivingRange/SwingSystem");

        // Ensure initial position is registered
        if (_swingSystem != null) CallDeferred(MethodName.RegisterInitialPosition);
    }

    private void RegisterInitialPosition()
    {
        if (_swingSystem != null) _swingSystem.SetTeePosition(GlobalPosition);
    }

    public override string GetInteractionPrompt()
    {
        return _isMoving ? "Left Click: Place Tee" : "E: Move Tee Box";
    }

    public override void OnInteract(PlayerController player)
    {
        // If we were not moving, start moving
        if (!_isMoving)
        {
            _isMoving = true;
            SetPhysics(false); // Disable collision so raycast doesn't hit self
        }
        else
        {
            // Stop moving (Place)
            _isMoving = false;
            SetPhysics(true);

            // Update Reset Position
            if (_swingSystem != null)
            {
                _swingSystem.SetTeePosition(GlobalPosition);
                // Also could reset player/ball here if desired
            }
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (_isMoving)
        {
            DoMoveLogic();

            // Allow Left Click to Place as well
            if (Input.IsMouseButtonPressed(MouseButton.Left))
            {
                // Simple debounce or verify
                _isMoving = false;
                SetPhysics(true);
                if (_swingSystem != null) _swingSystem.SetTeePosition(GlobalPosition);
            }
        }
    }

    private void DoMoveLogic()
    {
        if (_camera == null) _camera = GetViewport().GetCamera3D();
        if (_camera == null) return;

        // Raycast from camera center to terrain
        // Layer 1 (Terrain) 
        var space = GetWorld3D().DirectSpaceState;
        var from = _camera.GlobalPosition;
        var to = from + -_camera.GlobalTransform.Basis.Z * 50.0f; // 50m reach

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1; // Terrain only

        var result = space.IntersectRay(query);
        if (result.Count > 0)
        {
            Vector3 hitPos = (Vector3)result["position"];
            GlobalPosition = hitPos;
        }
        else
        {
            // If no terrain hit (sky), carry at fixed distance
            GlobalPosition = from + -_camera.GlobalTransform.Basis.Z * 3.0f;
        }
    }

    private void SetPhysics(bool enabled)
    {
        // Toggle collision layer to prevent self-intersection during raycasts
        var collider = FindChild("CollisionShape3D", true, false)?.GetParent() as CollisionObject3D;
        if (collider != null)
        {
            collider.CollisionLayer = enabled ? (uint)3 : 0; // Layer 1 & 2
        }
    }
}
