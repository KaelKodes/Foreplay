using Godot;
using System;

public partial class ObjectPlacer : Node
{
    private SwingSystem _swingSystem;
    private InteractableObject _currentObject;
    private Vector3 _originalPosition;
    private Vector3 _originalRotation; // Vector3 (Euler)
    private bool _isNewObject = false;

    // Placement State
    private float _currentRotationY = 0.0f;
    private float _currentHeightOffset = 0.0f;

    public bool IsPlacing => _currentObject != null;

    public override void _Ready()
    {
        _swingSystem = GetParent<SwingSystem>();
    }

    public void StartPlacing(InteractableObject obj)
    {
        if (obj == null) return;

        _currentObject = obj;
        _originalPosition = obj.GlobalPosition;
        _originalRotation = obj.GlobalRotation;

        _currentRotationY = obj.GlobalRotation.Y;
        _currentHeightOffset = 0.0f;

        // Notify SwingSystem/Player to enter placement mode
        var player = _swingSystem.GetNodeOrNull<PlayerController>("../PlayerPlaceholder"); // Assuming standard path
        if (player == null) player = _swingSystem.GetTree().GetFirstNodeInGroup("player") as PlayerController;

        if (player != null)
        {
            player.CurrentState = PlayerState.PlacingObject;
        }
    }

    public void SpawnAndPlace(PackedScene scene)
    {
        if (scene == null) return;
        var obj = scene.Instantiate<InteractableObject>();
        GetTree().CurrentScene.AddChild(obj);

        StartPlacing(obj);
        _isNewObject = true;

        // Position it roughly in front of camera to start
        var camera = GetViewport().GetCamera3D();
        if (camera != null)
        {
            obj.GlobalPosition = camera.GlobalPosition + camera.ProjectRayNormal(GetViewport().GetMousePosition()) * 5.0f;
        }
    }

    public void ConfirmPlacement()
    {
        if (_currentObject == null) return;

        GD.Print($"ObjectPlacer: Placed {_currentObject.Name} at {_currentObject.GlobalPosition}");
        _currentObject = null;
        _isNewObject = false;

        ExitPlacementMode();
    }

    public void CancelPlacement()
    {
        if (_currentObject == null) return;

        if (_isNewObject)
        {
            _currentObject.QueueFree();
        }
        else
        {
            // Revert
            _currentObject.GlobalPosition = _originalPosition;
            _currentObject.GlobalRotation = _originalRotation;
        }

        GD.Print($"ObjectPlacer: Cancelled placement of {_currentObject.Name}");
        _currentObject = null;
        _isNewObject = false;

        ExitPlacementMode();
    }

    private void ExitPlacementMode()
    {
        var player = _swingSystem.GetNodeOrNull<PlayerController>("../PlayerPlaceholder");
        if (player == null) player = _swingSystem.GetTree().GetFirstNodeInGroup("player") as PlayerController;

        if (player != null)
        {
            player.CurrentState = PlayerState.WalkMode; // Return to moving
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_currentObject == null) return;

        // Perform Raycast
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;

        var mousePos = GetViewport().GetMousePosition();
        var from = camera.ProjectRayOrigin(mousePos);
        var to = from + camera.ProjectRayNormal(mousePos) * 100.0f;

        var spaceState = _currentObject.GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 3; // Layer 1 (Default/Rough) + Layer 2 (Terrain/Heightmap)

        // Exclude the object's children to prevent self-intersection
        var exclude = new Godot.Collections.Array<Rid>();

        // Note: _currentObject is a Node3D, so we only check children for colliders
        var kids = _currentObject.FindChildren("*", "CollisionObject3D", true, false);
        foreach (var k in kids)
        {
            if (k is CollisionObject3D childCol) exclude.Add(childCol.GetRid());
        }
        query.Exclude = exclude;

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            Vector3 hitPos = (Vector3)result["position"];

            // Apply Height Offset
            Vector3 targetPos = hitPos + new Vector3(0, _currentHeightOffset, 0);

            _currentObject.GlobalPosition = _currentObject.GlobalPosition.Lerp(targetPos, 20.0f * (float)delta);

            // Apply Rotation
            Vector3 currentRot = _currentObject.GlobalRotation;
            _currentObject.GlobalRotation = new Vector3(currentRot.X, _currentRotationY, currentRot.Z);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_currentObject == null) return;

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                ConfirmPlacement();
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                CancelPlacement();
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                if (mb.ShiftPressed)
                    _currentHeightOffset += 0.1f;
                else
                    _currentRotationY += Mathf.DegToRad(15.0f);

                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                if (mb.ShiftPressed)
                    _currentHeightOffset -= 0.1f;
                else
                    _currentRotationY -= Mathf.DegToRad(15.0f);

                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventKey k && k.Pressed)
        {
            if (k.Keycode == Key.Escape)
            {
                CancelPlacement();
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
