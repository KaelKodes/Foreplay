using Godot;
using System;
using System.Collections.Generic;

namespace Foreplay.Data
{
    // Serializable Data Structures for Course Saving

    [Serializable]
    public class CourseData
    {
        public string CourseName { get; set; } = "New Course";
        public string Author { get; set; } = "Unknown";
        public long Timestamp { get; set; }

        public TerrainData Terrain { get; set; }
        public List<LevelObjectData> Objects { get; set; } = new List<LevelObjectData>();
    }

    [Serializable]
    public class TerrainData
    {
        public int Width { get; set; }
        public int Depth { get; set; }
        public float CellSize { get; set; }

        // Flattened arrays for JSON simplicity
        public float[] Heights { get; set; }
        public int[] Types { get; set; }
    }

    [Serializable]
    public class LevelObjectData
    {
        public string ObjectName { get; set; } // Display Name
        public string NodeName { get; set; } // Scene Tree Name (Crucial for lookups)
        public string ScenePath { get; set; } // "res://..." (optional, or use ID lookup)
        public string ModelPath { get; set; } // For runtime-wrapped GLTF objects

        // Transform
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }

        public float RotX { get; set; }
        public float RotY { get; set; }
        public float RotZ { get; set; }

        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public float ScaleZ { get; set; }

        // Metadata (e.g., text on signs)
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }
}
