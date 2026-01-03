using Godot;
using System;

public partial class DistanceMarker : InteractableObject
{
    [Export] public string Text = "100y";
    [Export] public Color TextColor = Colors.Black;
    [Export] public bool DynamicDistance = true;

    private Node3D _origin;
    private Label3D _label;
    private Vector3 _lastPos;

    public override void _Ready()
    {
        base._Ready();
        _label = GetNodeOrNull<Label3D>("Board/Label3D") ?? GetNodeOrNull<Label3D>("Label3D");

        CallDeferred(MethodName.FindTarget);
    }

    private void FindTarget()
    {
        // Strictly prioritize Tee for range markers
        if (_origin == null) _origin = GetTree().CurrentScene.FindChild("VisualTee", true, false) as Node3D;
        if (_origin == null) _origin = GetTree().CurrentScene.FindChild("TeeBox", true, false) as Node3D;
        if (_origin == null) _origin = GetTree().CurrentScene.FindChild("Tee", true, false) as Node3D;

        // Fallback for custom placed tees in building mode
        if (_origin == null)
        {
            var targets = GetTree().GetNodesInGroup("targets");
            foreach (Node n in targets)
            {
                if (n is Node3D n3d && n.Name.ToString().ToLower().Contains("tee"))
                {
                    _origin = n3d;
                    break;
                }
            }
        }

        if (_origin == null)
        {
            GD.PrintErr($"[DistanceMarker] {Name}: Could not find Tee origin in scene!");
        }
        else
        {
            UpdateDistance();
        }

        if (_label != null)
        {
            _label.Modulate = TextColor;
            UpdateDistance();
        }
    }

    public void UpdateDistance()
    {
        if (!DynamicDistance || _origin == null || _label == null) return;

        float dist = GlobalPosition.DistanceTo(_origin.GlobalPosition);
        // Using locked ratio from constants
        float yards = dist * Golf.GolfConstants.UNIT_RATIO;

        _label.Text = $"{Mathf.RoundToInt(yards)}y";
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        // Update distance every frame if being moved (pulse check) or if selected
        // We check Scale to see if we are pulsing (Selection effect) or just use a more direct check
        if (DynamicDistance && (IsSelected || GlobalPosition.DistanceSquaredTo(_lastPos) > 0.001f || Engine.GetFramesDrawn() % 60 == 0))
        {
            UpdateDistance();
            _lastPos = GlobalPosition;
        }
    }
}
