using Godot;
using System;

public partial class MainMenuController : Control
{
    private MenuPhysicsHelper _physicsHelper;

    public override void _Ready()
    {
        // Setup Physics Helper
        _physicsHelper = new MenuPhysicsHelper();
        AddChild(_physicsHelper);

        // Wiring
        _physicsHelper.BallScene = GD.Load<PackedScene>("res://Scenes/Entities/GolfBall.tscn");
        _physicsHelper.BallsContainer = GetNode<Node3D>("BallViewport/SubViewport/MenuStage/BallsContainer");
        _physicsHelper.CollidersContainer = GetNode<Node3D>("BallViewport/SubViewport/MenuStage/CollidersContainer");
        _physicsHelper.StageCamera = GetNode<Camera3D>("BallViewport/SubViewport/MenuStage/Camera3D");

        // Delay collider generation to ensure UI layout is final
        CallDeferred(MethodName.InitPhysics);
    }

    private void InitPhysics()
    {
        _physicsHelper.RefreshColliders();
    }

    private void OnDrivingRangePressed()
    {
        // Redirecting to TerrainTest for development
        // GetTree().ChangeSceneToFile("res://Scenes/Levels/DrivingRange.tscn");
        GetTree().ChangeSceneToFile("res://Scenes/Levels/TerrainTest.tscn");
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
