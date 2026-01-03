using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Foreplay.Data;

public partial class CoursePersistenceManager
{
    private static CoursePersistenceManager _instance;
    public static CoursePersistenceManager Instance
    {
        get
        {
            if (_instance == null) _instance = new CoursePersistenceManager();
            return _instance;
        }
    }

    public static string CourseToLoad { get; set; }

    private const string SAVE_DIR = "user://courses/";

    public CoursePersistenceManager()
    {
        DirAccess.MakeDirAbsolute(SAVE_DIR);
    }

    // --- SAVE ---

    public void SaveCourse(string filename, HeightmapTerrain terrain, Node rootNode)
    {
        CourseData data = new CourseData();
        data.CourseName = filename;
        data.Author = "Player"; // TODO: Get from profile
        data.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 1. Capture Terrain
        if (terrain != null)
        {
            data.Terrain = new TerrainData();
            data.Terrain.Width = terrain.GridWidth;
            data.Terrain.Depth = terrain.GridDepth;
            data.Terrain.CellSize = terrain.CellSize;

            int count = (terrain.GridWidth + 1) * (terrain.GridDepth + 1);
            data.Terrain.Heights = new float[count];
            data.Terrain.Types = new int[count];

            // We need access to terrain data arrays. 
            // Assuming HeightmapTerrain has public access or we added Get methods.
            // Using Get methods I will add momentarily:
            var hData = terrain.GetHeightData();
            var tData = terrain.GetTerrainTypeData();

            int idx = 0;
            for (int z = 0; z <= terrain.GridDepth; z++)
            {
                for (int x = 0; x <= terrain.GridWidth; x++)
                {
                    data.Terrain.Heights[idx] = hData[x, z];
                    data.Terrain.Types[idx] = tData[x, z];
                    idx++;
                }
            }
        }

        // 2. Capture Objects
        // Find all InteractableObjects in the scene
        // We scan the root node recursive or group
        var objects = rootNode.FindChildren("*", "Node3D", true, false);
        // Better: Use a group "saveable" or just Type InteractableObject

        foreach (var node in objects)
        {
            // Debug Loop
            // GD.Print($"Checking node: {node.Name} Type: {node.GetType()} Path: {node.SceneFilePath}");
            LevelObjectData objData = null;

            if (node is InteractableObject obj)
            {
                // Check if it's a wrapper with a GLTF model
                if (string.IsNullOrEmpty(obj.SceneFilePath))
                {
                    foreach (var child in obj.GetChildren())
                    {
                        if (!string.IsNullOrEmpty(child.SceneFilePath) &&
                           (child.SceneFilePath.EndsWith(".gltf") || child.SceneFilePath.EndsWith(".glb")))
                        {
                            objData = new LevelObjectData();
                            objData.ObjectName = obj.ObjectName;
                            objData.ModelPath = child.SceneFilePath;
                            break;
                        }
                    }
                    // If still null and not deletable/permanent, skip
                    if (objData == null && !obj.IsDeletable) continue;
                }
                else
                {
                    // Standard Packed Scene
                    objData = new LevelObjectData();
                    objData.ObjectName = obj.ObjectName;
                }
            }
            else if (node is TargetGreen green)
            {
                objData = new LevelObjectData();
                objData.ObjectName = green.GreenName;
            }
            else if (!string.IsNullOrEmpty(node.SceneFilePath) && node.GetParent()?.Name == "Trees")
            {
                // Generic Object (Trees)
                objData = new LevelObjectData();
                objData.ObjectName = node.Name;
            }

            if (objData != null && node is Node3D node3d)
            {
                objData.NodeName = node.Name;
                objData.ScenePath = node.SceneFilePath; // Crucial for Packed Scene saving
                if (string.IsNullOrEmpty(objData.ScenePath))
                {
                    // Fallback for runtime created objects? 
                    // For now, logging warning if it's supposed to be saved.
                    // GD.PrintErr($"Save: Object {node.Name} has no ScenePath!");
                }

                objData.PosX = node3d.GlobalPosition.X;
                objData.PosY = node3d.GlobalPosition.Y;
                objData.PosZ = node3d.GlobalPosition.Z;

                objData.RotX = node3d.GlobalRotation.X;
                objData.RotY = node3d.GlobalRotation.Y;
                objData.RotZ = node3d.GlobalRotation.Z;

                objData.ScaleX = node3d.Scale.X;
                objData.ScaleY = node3d.Scale.Y;
                objData.ScaleZ = node3d.Scale.Z;

                data.Objects.Add(objData);
            }
        }

        // Serialize
        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        string path = SAVE_DIR + filename + ".json";

        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        file.StoreString(json);

        GD.Print($"CoursePersistenceManager: Saved course to {path}");
    }

    // --- LOAD ---

    public bool LoadCourse(string filename, HeightmapTerrain terrain, Node rootNode)
    {
        string path = SAVE_DIR + filename + ".json";
        if (!Godot.FileAccess.FileExists(path))
        {
            GD.PrintErr($"CoursePersistenceManager: Save file not found: {path}");
            return false;
        }

        string json = "";
        using (var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read))
        {
            json = file.GetAsText();
        }

        try
        {
            CourseData data = JsonSerializer.Deserialize<CourseData>(json);

            // 1. Restore Terrain
            if (terrain != null && data.Terrain != null)
            {
                // Verify dimensions (or resize terrain?)
                // For simplified V1: Assume playing on compatible grid or current grid.
                // We should probably resize terrain to match save.

                int idx = 0;
                for (int z = 0; z <= terrain.GridDepth; z++)
                {
                    for (int x = 0; x <= terrain.GridWidth; x++)
                    {
                        if (idx < data.Terrain.Heights.Length)
                        {
                            terrain.SetData(x, z, data.Terrain.Heights[idx], data.Terrain.Types[idx]);
                        }
                        idx++;
                    }
                }
                terrain.UpdateMesh();
            }

            // 2. Restore Objects
            // First, clear existing dynamic objects?
            // Get all InteractableObjects and QueueFree() them?
            var existing = rootNode.FindChildren("*", "Node3D", true, false);
            foreach (var n in existing)
            {
                if (n is InteractableObject obj && obj.IsDeletable)
                {
                    obj.QueueFree();
                }
            }

            // Spawn new
            foreach (var objData in data.Objects)
            {
                // Resolve path
                // If ObjectName is "Pine_1", reconstruct path or look up asset list.
                // If ScenePath is saved, use it.

                string spawnPath = "";
                if (!string.IsNullOrEmpty(objData.ScenePath))
                {
                    spawnPath = objData.ScenePath;
                }
                else if (!string.IsNullOrEmpty(objData.ModelPath))
                {
                    spawnPath = objData.ModelPath;
                }
                else
                {
                    // Fallback to name-based lookup
                    spawnPath = FindAssetPath(objData.ObjectName);
                }

                if (string.IsNullOrEmpty(spawnPath)) continue;

                var scene = GD.Load<PackedScene>(spawnPath);
                if (scene != null)
                {
                    // Instantiate
                    Node3D node = null;

                    // Helper similar to HUD logic to wrap GLTF or use Scene
                    if (!string.IsNullOrEmpty(objData.ModelPath) || spawnPath.EndsWith(".gltf") || spawnPath.EndsWith(".glb"))
                    {
                        var model = scene.Instantiate();
                        var container = new InteractableObject(); // Base wrapper
                        container.ObjectName = objData.ObjectName;
                        container.AddChild(model);

                        // Add collider helper if needed (borrowed from HUD logic)
                        // Verify if model already has collision? Likely not for GLTF.
                        var staticBody = new StaticBody3D();
                        var col = new CollisionShape3D();
                        var sphere = new SphereShape3D();
                        sphere.Radius = 1.0f;
                        col.Shape = sphere;
                        staticBody.AddChild(col);
                        container.AddChild(staticBody);

                        node = container;
                    }
                    else
                    {
                        node = scene.Instantiate<Node3D>();
                    }

                    rootNode.AddChild(node);

                    // Set Transform
                    node.GlobalPosition = new Vector3(objData.PosX, objData.PosY, objData.PosZ);
                    node.GlobalRotation = new Vector3(objData.RotX, objData.RotY, objData.RotZ);
                    node.Scale = new Vector3(objData.ScaleX, objData.ScaleY, objData.ScaleZ);

                    if (!string.IsNullOrEmpty(objData.NodeName))
                    {
                        node.Name = objData.NodeName;
                    }

                    if (node is InteractableObject io)
                    {
                        io.ObjectName = objData.ObjectName;
                    }
                    else if (node is TargetGreen green)
                    {
                        green.GreenName = objData.ObjectName;
                    }
                }
            }

            return true;
        }
        catch (Exception e)
        {
            GD.PrintErr($"CoursePersistenceManager: Failed to load: {e.Message}");
            return false;
        }
    }

    // Helper to map simplified names to paths (duplicated logic from HUD, sorry)
    private string FindAssetPath(string name)
    {
        if (name.Contains("Pine")) return $"res://Assets/Textures/Objects/{name}.gltf"; // Hacky assumption
        if (name.Contains("CommonTree")) return $"res://Assets/Textures/Objects/{name}.gltf";
        if (name == "CourseMap") return "res://Scenes/Environment/CourseMapSign.tscn";
        // ... expand as needed or make a shared AssetManager
        return "";
    }
}
