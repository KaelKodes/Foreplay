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

    public override void _Ready()
    {
        // Try to find a mesh for highlighting
        _mesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (_mesh == null)
        {
            // Fallback: Check children
            foreach (Node child in GetChildren())
            {
                if (child is MeshInstance3D m)
                {
                    _mesh = m;
                    break;
                }
            }
        }
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

        // Reset scale if deselected
        if (!selected) Scale = Vector3.One;
    }

    private void UpdateVisuals(Color color, bool isSelected = false)
    {
        if (_mesh == null) return;

        // Use MaterialOverride to avoid leaking materials
        var mat = _mesh.GetActiveMaterial(0) as StandardMaterial3D;
        if (mat != null)
        {
            var uniqueMat = (StandardMaterial3D)mat.Duplicate();
            uniqueMat.AlbedoColor = color;

            if (isSelected)
            {
                uniqueMat.EmissionEnabled = true;
                uniqueMat.Emission = color;
                uniqueMat.EmissionEnergyMultiplier = 2.0f;
            }
            else
            {
                uniqueMat.EmissionEnabled = false;
            }

            _mesh.MaterialOverride = uniqueMat;
        }
    }

    public override void _Process(double delta)
    {
        if (IsSelected)
        {
            // Subtle pulse effect
            float pulse = 1.0f + (Mathf.Sin((float)Time.GetTicksMsec() * 0.01f) * 0.05f);
            Scale = new Vector3(pulse, pulse, pulse);
        }
    }
}
