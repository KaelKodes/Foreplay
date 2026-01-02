using Godot;
using System;

public partial class TargetGreen : Area3D
{
    [Export] public string GreenName = "Target Green";

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node body)
    {
        if (body is BallController ball)
        {
            GD.Print($"🎯 BULLSEYE! Landed on {GreenName}!");
            // Future: Trigger score / sound
        }
    }
}
