using Godot;
using System;

public partial class CourseMapSign : InteractableObject
{
    private SubViewport _viewport;
    private Camera3D _camera;
    private MeshInstance3D _mapFace;

    public override void _Ready()
    {
        base._Ready(); // Initialize InteractableObject stuff

        _viewport = GetNode<SubViewport>("SubViewport");
        _camera = _viewport.GetNode<Camera3D>("Camera3D");
        _mapFace = GetNode<MeshInstance3D>("Board/MapFace");

        SetupCamera();
        SetupMaterial();
    }

    private void SetupCamera()
    {
        // Find terrain
        var terrain = GetTree().GetFirstNodeInGroup("terrain") as HeightmapTerrain;
        if (terrain != null)
        {
            float width = terrain.GridWidth * terrain.CellSize;
            float depth = terrain.GridDepth * terrain.CellSize;

            // Center camera relative to terrain
            // Assuming terrain origin is at 0,0,0 or we use its GlobalPosition
            Vector3 center = terrain.GlobalPosition + new Vector3(width / 2.0f, 100.0f, depth / 2.0f);

            // We set GlobalPosition, but since Camera is in a SubViewport,
            // we need to make sure the SubViewport doesn't reset it or use local transforms weirdly.
            // Actually, for a pure "Map" camera, it doesn't need to be a child of the sign physically in 3D space,
            // but for scene organization it is. 
            // We must ensure the Camera's Transform is set to World Space coordinates.
            _camera.TopLevel = true; // Detach from parent transform so we can place it globally
            _camera.GlobalPosition = center;
            _camera.GlobalRotationDegrees = new Vector3(-90, 180, 0); // Look down, rotated 180 to put Tee at bottom

            // Set ortho size to fit the larger dimension
            // Orthographic size is the vertical height of the view volume in meters
            // If aspect ratio is 1:1, width = size.
            // If board is rectangular, we might want to adjust.
            // Let's assume square or fit to max dimension
            float maxDim = Mathf.Max(width, depth);
            _camera.Projection = Camera3D.ProjectionType.Orthogonal;
            _camera.Size = maxDim * 1.2f; // 20% padding

            GD.Print($"[CourseMapSign] Camera setup at {center}, Size: {_camera.Size}");
        }
        else
        {
            GD.PrintErr("[CourseMapSign] No 'terrain' group found!");
        }
    }

    private void SetupMaterial()
    {
        // Create dynamic material
        var mat = new StandardMaterial3D();
        mat.AlbedoTexture = _viewport.GetTexture();
        mat.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded; // Map should be bright / visible without lighting artifacts

        // Apply to board
        _mapFace.MaterialOverride = mat;
    }

    public override string GetInteractionPrompt()
    {
        return "PRESS E TO MANAGE COURSE";
    }

    public override void OnInteract(PlayerController player)
    {
        // Find HUD
        var hud = GetTree().CurrentScene.FindChild("HUD", true, false) as MainHUDController;
        if (hud != null)
        {
            hud.ShowSaveLoadMenu();
        }
    }
}
