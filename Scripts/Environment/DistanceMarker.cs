using Godot;
using System;

public partial class DistanceMarker : Node3D
{
    [Export] public string Text = "100y";
    [Export] public Color TextColor = Colors.Black;

    public override void _Ready()
    {
        var label = GetNode<Label3D>("Board/Label3D");
        if (label != null)
        {
            label.Text = Text;
            label.Modulate = TextColor;
        }
    }
}
