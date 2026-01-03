using Godot;
using System;

public partial class DistanceMarker : InteractableObject
{
    [Export] public string Text = "100y";
    [Export] public Color TextColor = Colors.Black;
    [Export] public bool DynamicDistance = true;

    private Node3D _pin;
    private Label3D _label;
    private Vector3 _lastPos;

    public override void _Ready()
    {
        base._Ready();
        _label = GetNodeOrNull<Label3D>("Board/Label3D") ?? GetNodeOrNull<Label3D>("Label3D");

        // Find all nodes in "targets" group
        var targets = GetTree().GetNodesInGroup("targets");
        if (targets.Count > 0)
        {
            Node3D best = null;
            float minDist = float.MaxValue;
            foreach (Node n in targets)
            {
                if (n is Node3D n3d)
                {
                    float d = GlobalPosition.DistanceSquaredTo(n3d.GlobalPosition);
                    if (d < minDist)
                    {
                        minDist = d;
                        best = n3d;
                    }
                }
            }
            _pin = best;
        }

        // Fallback to name search if no group targets found
        if (_pin == null) _pin = GetTree().CurrentScene.FindChild("VisualTee", true, false) as Node3D;
        if (_pin == null) _pin = GetTree().CurrentScene.FindChild("TeeBox", true, false) as Node3D;
        if (_pin == null) _pin = GetTree().CurrentScene.FindChild("Pin", true, false) as Node3D;
        if (_pin == null) _pin = GetTree().CurrentScene.FindChild("Tee", true, false) as Node3D;

        if (_pin == null)
        {
            GD.PrintErr($"[DistanceMarker] {Name}: Could not find Pin or Tee target in scene!");
        }
        else
        {
            GD.Print($"[DistanceMarker] {Name}: Target found -> {_pin.Name}");
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
        if (!DynamicDistance || _pin == null || _label == null) return;

        float dist = GlobalPosition.DistanceTo(_pin.GlobalPosition);
        // Using "x2 logic" for perceived yards as requested
        float yards = dist * 2.0f;

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
