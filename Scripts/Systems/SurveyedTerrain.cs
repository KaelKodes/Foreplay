using Godot;
using System.Collections.Generic;

public partial class SurveyedTerrain : CsgPolygon3D
{
    [Export] public Vector3[] Points { get; set; } = System.Array.Empty<Vector3>();
    [Export] public int TerrainType { get; set; } = 0;

    public override void _Ready()
    {
        AddToGroup("surveyed_terrain");
    }
}
