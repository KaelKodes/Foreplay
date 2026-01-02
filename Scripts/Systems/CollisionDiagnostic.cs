using Godot;

// Diagnostic script to test collision detection
// Attach this to a Node3D in the scene to test raycast collision
public partial class CollisionDiagnostic : Node3D
{
    public override void _Ready()
    {
        // Wait a frame for physics to initialize
        CallDeferred(nameof(TestCollision));
    }

    private void TestCollision()
    {
        var spaceState = GetWorld3D().DirectSpaceState;

        // Test raycast from above the terrain downward
        var from = new Vector3(0, 10, 0);
        var to = new Vector3(0, -10, 0);

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 2; // Looking for layer 2 (terrain)

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            Vector3 hitPos = (Vector3)result["position"];
            Vector3 hitNormal = (Vector3)result["normal"];
            GD.Print($"✅ COLLISION FOUND!");
            GD.Print($"   Hit Position: {hitPos}");
            GD.Print($"   Hit Normal: {hitNormal}");
            GD.Print($"   Normal pointing UP? {hitNormal.Y > 0.9f}");
        }
        else
        {
            GD.Print($"❌ NO COLLISION DETECTED");
            GD.Print($"   Raycast from {from} to {to}");
            GD.Print($"   Looking for collision layer 2");
        }
    }
}
