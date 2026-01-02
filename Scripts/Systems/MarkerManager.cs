using Godot;
using System.Collections.Generic;

public partial class MarkerManager : Node
{
    public List<MeshInstance3D> GhostMarkers { get; } = new List<MeshInstance3D>();
    private MeshInstance3D _ballIndicator;

    public void UpdateBallIndicator(bool visible, Vector3 ballPosition, int playerIndex)
    {
        if (visible)
        {
            if (_ballIndicator == null)
            {
                _ballIndicator = new MeshInstance3D();
                var prism = new PrismMesh();
                prism.Size = new Vector3(0.3f, 0.5f, 0.1f);
                _ballIndicator.Mesh = prism;
                _ballIndicator.MaterialOverride = new StandardMaterial3D();
                AddChild(_ballIndicator);
            }

            Color c = GetPlayerColor(playerIndex);
            ((StandardMaterial3D)_ballIndicator.MaterialOverride).AlbedoColor = c;

            _ballIndicator.GlobalPosition = ballPosition + new Vector3(0, 1.5f, 0);
            _ballIndicator.RotationDegrees = new Vector3(180, 0, 0); // Point Down
            _ballIndicator.Visible = true;
        }
        else
        {
            if (_ballIndicator != null) _ballIndicator.Visible = false;
        }
    }

    public void CreateGhostMarker(Vector3 position, int playerIndex)
    {
        var ghost = new MeshInstance3D();
        var prism = new PrismMesh();
        prism.Size = new Vector3(0.3f, 0.5f, 0.1f);
        ghost.Mesh = prism;

        var mat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        Color c = GetPlayerColor(playerIndex);
        mat.AlbedoColor = c.Lerp(Colors.Gray, 0.7f);
        mat.AlbedoColor = new Color(mat.AlbedoColor.R, mat.AlbedoColor.G, mat.AlbedoColor.B, 0.6f);

        ghost.MaterialOverride = mat;
        AddChild(ghost);

        ghost.GlobalPosition = position;
        ghost.RotationDegrees = new Vector3(180, 0, 0);
        GhostMarkers.Add(ghost);
    }

    public void ClearGhostMarkers()
    {
        foreach (var ghost in GhostMarkers)
        {
            if (IsInstanceValid(ghost)) ghost.QueueFree();
        }
        GhostMarkers.Clear();
    }

    public void SetGhostMarkersVisible(bool visible)
    {
        foreach (var ghost in GhostMarkers)
        {
            if (IsInstanceValid(ghost)) ghost.Visible = visible;
        }
    }

    private Color GetPlayerColor(int index)
    {
        return (index % 4) switch
        {
            0 => Colors.Blue,
            1 => Colors.Red,
            2 => Colors.Green,
            3 => Colors.Yellow,
            _ => Colors.Blue
        };
    }
}
