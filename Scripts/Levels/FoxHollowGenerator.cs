using Godot;
using System;
using System.Collections.Generic;

public partial class FoxHollowGenerator : Node
{
    [Export] public NodePath TerrainPath;
    [Export] public NodePath ObjectPlacerPath;
    [Export] public NodePath PlayerPath;
    [Export] public PackedScene TreeScene;

    private HeightmapTerrain _terrain;
    private ObjectPlacer _objectPlacer; // Optional, for spawning objects if needed properly

    // Config
    private float _holeLength = 310f; // 340 yards approx
    private float _fairwayWidth = 20f; // Half width
    private float _roughWidth = 35f; // Half width

    // Curve Control Points (relative to hole start at 0,0)
    // Start: 0,0
    // Mid: -20, 150 (Slight left curve)
    // End: -40, 310

    private Vector2 _p0 = new Vector2(100, 20); // Tee (centered in 200 width grid)
    private Vector2 _p1 = new Vector2(80, 150); // Mid curve
    private Vector2 _p2 = new Vector2(60, 310); // Green

    public override void _Ready()
    {
        // Defer generation to ensure Terrain is ready
        CallDeferred(MethodName.GenerateHole);
    }

    private void GenerateHole()
    {
        _terrain = GetNodeOrNull<HeightmapTerrain>(TerrainPath);
        if (_terrain == null)
        {
            GD.PrintErr("FoxHollowGenerator: No terrain found!");
            return;
        }

        // Check for Load Request
        if (!string.IsNullOrEmpty(CoursePersistenceManager.CourseToLoad))
        {
            GD.Print($"FoxHollowGenerator: Loading course {CoursePersistenceManager.CourseToLoad}...");
            bool success = CoursePersistenceManager.Instance.LoadCourse(CoursePersistenceManager.CourseToLoad, _terrain, this);
            if (success)
            {
                CoursePersistenceManager.CourseToLoad = null; // Clear request
                return; // SKIP procedural gen
            }
            GD.PrintErr("Failed to load course, falling back to procedural generation.");
        }

        GD.Print("FoxHollowGenerator: Generating Hole 1...");

        // sculpted heights
        for (int z = 0; z <= _terrain.GridDepth; z++)
        {
            for (int x = 0; x <= _terrain.GridWidth; x++)
            {
                float wx = x * _terrain.CellSize;
                float wz = z * _terrain.CellSize;

                // Default: Heavy Rough / Woods
                int type = 1; // Rough (Dark Green)
                float height = 0f;

                // Calculate distance to fairway curve
                float t = wz / _holeLength;
                t = Mathf.Clamp(t, 0, 1);

                // Quadratic Bezier for fairway center
                Vector2 curvePos = _p0.Lerp(_p1, t).Lerp(_p1.Lerp(_p2, t), t);

                float dist = Mathf.Abs(wx - curvePos.X);

                // Add some noise to edges
                float noise = (float)GD.RandRange(-2.0, 2.0);

                if (dist < _fairwayWidth + noise)
                {
                    type = 0; // Fairway
                    height = 0.5f; // Slight crown
                }
                else if (dist < _roughWidth + noise)
                {
                    type = 1; // Rough
                    height = 0.8f; // Rough slightly higher
                }
                else
                {
                    type = 1; // Heavy Rough (Visualized as Rough for now, relying on Trees)
                    height = 1.5f + (float)GD.RandRange(0, 0.5); // Mounds
                }

                // Green Area (Circle at P2)
                float distToGreen = new Vector2(wx, wz).DistanceTo(_p2);
                if (distToGreen < 15f)
                {
                    type = 0; // Green (Fairway type for now, maybe specialized later)
                    height = 1.0f; // Green platform

                    // Flatten green
                    height = 1.0f + (Mathf.Sin(wx * 0.1f) * 0.1f); // Micro undulation
                }

                // Bunker (Front Left of Green)
                // Green is at _p2. Front Left relative to approach means smaller Z, smaller X (since curving left)
                Vector2 bunkerPos = _p2 + new Vector2(-15, -15);
                float distToBunker = new Vector2(wx, wz).DistanceTo(bunkerPos);
                if (distToBunker < 6f)
                {
                    type = 4; // Sand
                    height = -0.5f; // Depressed
                }

                // Tee Box
                if (wz < 10 && Mathf.Abs(wx - _p0.X) < 5)
                {
                    type = 0; // Fairway/Tee
                    height = 0.2f; // Flat
                }

                _terrain.SetData(x, z, height, type);
            }
        }

        _terrain.UpdateMesh();

        // Spawn Trees
        SpawnTrees();

        GD.Print("FoxHollowGenerator: Complete.");
    }

    private void SpawnTrees()
    {
        var treesNode = new Node3D { Name = "Trees" };
        AddChild(treesNode);

        for (int i = 0; i < 400; i++)
        {
            float z = (float)GD.RandRange(0, _holeLength + 20);
            float t = z / _holeLength;
            Vector2 curvePos = _p0.Lerp(_p1, t).Lerp(_p1.Lerp(_p2, t), t);

            // Pick side
            float side = GD.Randf() > 0.5f ? 1f : -1f;
            float dist = (float)GD.RandRange(_roughWidth + 5, _roughWidth + 40);

            float x = curvePos.X + (side * dist);

            // Check bounds
            if (x < 0 || x > _terrain.GridWidth * _terrain.CellSize) continue;

            // Check bunker/green
            if (new Vector2(x, z).DistanceTo(_p2) < 20) continue;

            // Place Tree
            string treeName = GD.Randf() > 0.5f ? $"Pine_{GD.RandRange(1, 5)}" : $"CommonTree_{GD.RandRange(1, 4)}"; // Using Pine 1-5, Common 1-4
            string path = $"res://Assets/Textures/Objects/{treeName}.gltf";
            var scene = GD.Load<PackedScene>(path);
            if (scene != null)
            {
                var tree = scene.Instantiate<Node3D>();
                tree.SceneFilePath = path; // Manually set path for persistence
                treesNode.AddChild(tree);

                // Approximate Y position
                float y = 1.0f;
                // If we updated the terrain data, we could query it, but simplistic 1.0f is close to rough height.

                tree.GlobalPosition = _terrain.GlobalPosition + new Vector3(x, y, z);
                tree.Scale = Vector3.One * (float)GD.RandRange(0.8, 1.2);
                tree.RotationDegrees = new Vector3(0, (float)GD.RandRange(0, 360), 0);
            }
        }

        SpawnTee();
        SpawnGreen();
        SpawnMapSign();
    }

    private void SpawnTee_Old()
    {
        // ... (Old method, can keep as is or ignore)
    }

    private void SpawnGreen()
    {
        // Load Green Complex
        string path = "res://Scenes/Environment/TargetGreen.tscn";
        var greenScene = GD.Load<PackedScene>(path);
        if (greenScene != null)
        {
            var green = greenScene.Instantiate<Node3D>();
            green.SceneFilePath = path; // Ensure persistence
            green.Name = "GreenComplex";
            AddChild(green);
            // ...

            // Position at P2 (Green Location)
            // Ensure height matches terrain or overrides it
            // The Terrain Gen sets green height to ~1.0f. TargetGreen has its own geometry.
            Vector3 greenPos = _terrain.GlobalPosition + new Vector3(_p2.X, 1.0f, _p2.Y);
            green.GlobalPosition = greenPos;

            // Fix: Rotate to align with fairway incoming angle?
            // Incoming vector: P1 -> P2.
            Vector3 incDir = new Vector3(_p2.X - _p1.X, 0, _p2.Y - _p1.Y).Normalized();
            float angle = Mathf.Atan2(incDir.X, incDir.Z);
            green.Rotation = new Vector3(0, angle, 0);

            // Ensure the PIN inside is findable
            // TargetGreen has a child named "Pin".
            // We should add it to "targets" group or rely on Name.
            // Pin.cs doesn't add itself, so we can do it here if we want robustness:
            var pin = green.FindChild("Pin", true, false);
            if (pin != null)
            {
                pin.AddToGroup("targets");
                // Also rename for specific lookup if needed, but Group is best.
                ((Node)pin).Name = "GreenPin"; // Force name for DistanceMarker fallback
            }
        }
        else
        {
            GD.PrintErr("FoxHollowGenerator: Failed to load TargetGreen.tscn");
        }
    }

    private void SpawnMapSign()
    {
        string path = "res://Scenes/Environment/CourseMapSign.tscn";
        var mapScene = GD.Load<PackedScene>(path);
        if (mapScene != null)
        {
            var map = mapScene.Instantiate<InteractableObject>();
            map.SceneFilePath = path; // Persistence
            map.Name = "CourseMap";
            map.ObjectName = "Course Map"; // Ensure name is set for interaction prompt
            AddChild(map);

            // Position near the Tee (_p0), slightly offset
            Vector3 teePos = _terrain.GlobalPosition + new Vector3(_p0.X, 0.2f, _p0.Y);
            // Move 3 meters left and 2 meters back
            Vector3 signPos = teePos + new Vector3(3.0f, 0, -2.0f);

            map.GlobalPosition = signPos;
            map.RotationDegrees = new Vector3(0, 180, 0); // Face the tee
        }
        else
        {
            GD.PrintErr("FoxHollowGenerator: Failed to load CourseMapSign.tscn");
        }
    }

    private void SpawnTee()
    {
        // Load proper TeeBox scene
        string path = "res://Scenes/Environment/TeeBox.tscn";
        var teeScene = GD.Load<PackedScene>(path);
        if (teeScene != null)
        {
            var tee = teeScene.Instantiate<Node3D>();
            tee.SceneFilePath = path; // Persistence
            tee.Name = "TeeBox";
            AddChild(tee);

            // Position at P0 (Tee Location)
            Vector3 teePos = _terrain.GlobalPosition + new Vector3(_p0.X, 0.5f, _p0.Y);
            tee.GlobalPosition = teePos;
        }
        else
        {
            GD.PrintErr("FoxHollowGenerator: Could not load TeeBox.tscn");
        }
    }
}
