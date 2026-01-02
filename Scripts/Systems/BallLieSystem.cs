using Godot;
using System;

public enum LieType
{
    Tee,
    Fairway,
    Rough,
    DeepRough,
    Sand,
    Green
}

public class BallLie
{
    public LieType Type { get; set; }
    public float PowerEfficiency { get; set; } = 1.0f;
    public float ControlModifier { get; set; } = 1.0f;
    public float SpinReliability { get; set; } = 1.0f;
    public float SpinModifier { get; set; } = 1.0f;
    public float LaunchAngleBonus { get; set; } = 0.0f;
    public float RollResistance { get; set; } = 0.05f;
}

public partial class BallLieSystem : Node
{
    public BallLie GetCurrentLie(Vector3 position)
    {
        // Simple implementation using RayCast3D or collision checks
        // For the prototype, we check the node names of the floor


        var spaceState = (GetViewport().GetWorld3D().DirectSpaceState);
        var query = PhysicsRayQueryParameters3D.Create(position + Vector3.Up * 0.5f, position + Vector3.Down * 0.5f);
        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            Node collider = (Node)result["collider"];
            string name = collider.Name.ToString().ToLower();

            if (name.Contains("tee")) return new BallLie { Type = LieType.Tee, PowerEfficiency = 1.05f, LaunchAngleBonus = 0.04f, SpinModifier = 0.9f, RollResistance = 0.02f };
            if (name.Contains("fairway")) return new BallLie { Type = LieType.Fairway, PowerEfficiency = 0.95f, RollResistance = 0.04f };
            if (name.Contains("rough")) return new BallLie { Type = LieType.Rough, PowerEfficiency = 0.7f, ControlModifier = 0.5f, RollResistance = 0.15f };
            if (name.Contains("sand")) return new BallLie { Type = LieType.Sand, PowerEfficiency = 0.6f, SpinReliability = 0.4f, RollResistance = 0.25f };
            if (name.Contains("green")) return new BallLie { Type = LieType.Green, PowerEfficiency = 1.0f, RollResistance = 0.015f };
        }

        return new BallLie { Type = LieType.Fairway }; // Default
    }
}
