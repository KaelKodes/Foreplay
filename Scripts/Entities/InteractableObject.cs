using Godot;
using System;

public partial class InteractableObject : Node3D
{
    [Export] public string ObjectName = "Object";
    [Export] public bool IsMovable = true;
    [Export] public bool IsDeletable = true;
    public bool IsSelected { get; private set; } = false;

    // Optional: Visual highlight
    private MeshInstance3D _mesh;
    private MeshInstance3D _gizmoRing;

    public override void _Ready()
    {
        // Try to find a mesh for highlighting
        _mesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (_mesh == null)
        {
            // Fallback: Check children recursively
            _mesh = FindMeshRecursive(this);
        }

        // Create Rotation Gizmo
        CreateGizmo();

        // Cache original material
        if (_mesh != null)
        {
            _originalMaterial = _mesh.GetActiveMaterial(0) as StandardMaterial3D;
        }
    }

    private StandardMaterial3D _originalMaterial;

    private void CreateGizmo()
    {
        _gizmoRing = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = 1.8f;
        torus.OuterRadius = 2.0f;
        _gizmoRing.Mesh = torus;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0, 1, 1, 0.5f); // Cyan transparent
        mat.EmissionEnabled = true;
        mat.Emission = Colors.Cyan;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.NoDepthTest = true; // Always visible on top
        _gizmoRing.MaterialOverride = mat;

        _gizmoRing.Visible = false;
        AddChild(_gizmoRing);
    }

    private MeshInstance3D FindMeshRecursive(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is MeshInstance3D m) return m;
            var found = FindMeshRecursive(child);
            if (found != null) return found;
        }
        return null;
    }

    public void OnHover(bool isHovered)
    {
        if (IsSelected) return; // Selection takes priority visually
        UpdateVisuals(isHovered ? new Color(1.5f, 1.5f, 1.5f) : Colors.White);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        UpdateVisuals(selected ? Colors.Cyan : Colors.White, selected);

        if (_gizmoRing != null) _gizmoRing.Visible = selected;
    }

    private void UpdateVisuals(Color color, bool isSelected = false)
    {
        if (_mesh == null) return;

        // Visual Reset
        if (!isSelected && color == Colors.White)
        {
            _mesh.MaterialOverride = null;
            return;
        }

        // Apply Highlight/Selection
        if (_originalMaterial != null)
        {
            var uniqueMat = (StandardMaterial3D)_originalMaterial.Duplicate();
            uniqueMat.AlbedoColor = color;

            if (isSelected)
            {
                uniqueMat.EmissionEnabled = true;
                uniqueMat.Emission = color;
                uniqueMat.EmissionEnergyMultiplier = 2.0f;
            }

            _mesh.MaterialOverride = uniqueMat;
        }
    }

    public override void _Process(double delta)
    {
        // Pulse removed.
    }
}
