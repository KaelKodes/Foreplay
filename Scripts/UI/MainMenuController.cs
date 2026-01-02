using Godot;
using System;

public partial class MainMenuController : Control
{
    private void OnDrivingRangePressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Levels/DrivingRange.tscn");
    }

    private void OnPuttingRangePressed()
    {
        // Placeholder for when PuttingRange scene is created
        GD.Print("Putting Range selected");
        // GetTree().ChangeSceneToFile("res://Scenes/Levels/PuttingRange.tscn");
    }

    private void OnExitPressed()
    {
        GetTree().Quit();
    }
}
