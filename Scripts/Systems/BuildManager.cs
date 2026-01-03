using Godot;
using System.Collections.Generic;

public partial class BuildManager : Node3D
{
    private List<Vector3> _points = new List<Vector3>();
    private List<Node3D> _markers = new List<Node3D>();
    private ImmediateMesh _lineMesh;
    private MeshInstance3D _lineInstance;
    private CsgPolygon3D _previewMeshInstance;

    public float CurrentElevation { get; private set; } = 0.0f;
    private float _fillPercentage = 80.0f;

    private CsgCombiner3D _csgRoot = null;

    private SurveyedTerrain _closestTerrain = null;
    private List<Vector3> _copiedPoints = null;

    public PlayerController Player;
    private SwingSystem _swingSystem;

    private int _closestMarkerIndex = -1;
    private int _replacingIndex = -1;
    private float _inputCooldown = 0.0f;
    public bool IsPickingTerrain = false;
    private int _lastSelectedType = 0;
    private MainHUDController _hud;

    public override void _Ready()
    {
        // SwingSystem is our parent (it creates us dynamically)
        _swingSystem = GetParent<SwingSystem>();

        // Setup immediate mesh for boundary line
        _lineInstance = new MeshInstance3D();
        _lineMesh = new ImmediateMesh();
        _lineInstance.Mesh = _lineMesh;

        var mat = new StandardMaterial3D();
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.AlbedoColor = Colors.Yellow;
        mat.NoDepthTest = true; // See it through terrain
        _lineInstance.MaterialOverride = mat;

        _hud = GetTree().CurrentScene.FindChild("HUD", true, false) as MainHUDController;

        AddChild(_lineInstance);
    }

    public void AddPoint(Vector3 position)
    {
        _points.Add(position);
        CreateMarker(position);
        UpdateLines();
    }

    private void CreateMarker(Vector3 position)
    {
        var root = new Node3D();
        AddChild(root);
        root.GlobalPosition = position + new Vector3(0, 0.1f, 0);

        var mesh = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = 0.2f;
        sphere.Height = 0.4f;
        mesh.Mesh = sphere;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = Colors.Yellow;
        mesh.MaterialOverride = mat;
        root.AddChild(mesh);

        // Add Label for counting
        var label = new Label3D();
        label.Name = "Label3D"; // Explicitly name it for lookup
        label.Text = (_points.Count).ToString();
        label.FontSize = 48;
        label.OutlineSize = 12;
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        label.Position = new Vector3(0, 0.5f, 0);
        root.AddChild(label);

        _markers.Add(root);
    }

    private void UpdateMarkerLabels()
    {
        for (int i = 0; i < _markers.Count; i++)
        {
            var label = _markers[i].GetNodeOrNull<Label3D>("Label3D");
            if (label != null) label.Text = (i + 1).ToString();
        }
    }

    public void UpdateLines()
    {
        _lineMesh.ClearSurfaces();
        if (_points.Count < 2) return;

        _lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (int i = 0; i < _points.Count - 1; i++)
        {
            _lineMesh.SurfaceAddVertex(_points[i] + new Vector3(0, 0.2f, 0));
            _lineMesh.SurfaceAddVertex(_points[i + 1] + new Vector3(0, 0.2f, 0));
        }

        // If we want a preview line to the player
        if (!IsPickingTerrain && Player != null && Player.CurrentState == PlayerState.BuildMode && _replacingIndex == -1)
        {
            _lineMesh.SurfaceAddVertex(_points[_points.Count - 1] + new Vector3(0, 0.2f, 0));
            _lineMesh.SurfaceAddVertex(Player.GlobalPosition + new Vector3(0, 0.2f, 0));

            // Also draw potential closing line from player to first point
            if (_points.Count >= 2)
            {
                _lineMesh.SurfaceAddVertex(Player.GlobalPosition + new Vector3(0, 0.2f, 0));
                _lineMesh.SurfaceAddVertex(_points[0] + new Vector3(0, 0.2f, 0));
            }
        }
        else if (_points.Count >= 3)
        {
            // Close the loop if not drawing to player
            _lineMesh.SurfaceAddVertex(_points[_points.Count - 1] + new Vector3(0, 0.2f, 0));
            _lineMesh.SurfaceAddVertex(_points[0] + new Vector3(0, 0.2f, 0));
        }

        _lineMesh.SurfaceEnd();
    }

    public void ModifyElevation(float delta)
    {
        CurrentElevation += delta;
        GD.Print($"BuildManager: Elevation modified to {CurrentElevation}");
        SetPreviewTerrain(_lastSelectedType);
    }

    public void SetFillPercentage(float pct)
    {
        _fillPercentage = pct;
        GD.Print($"BuildManager: Fill percentage set to {_fillPercentage}%");
        SetPreviewTerrain(_lastSelectedType);
    }

    public override void _Process(double delta)
    {
        if (_inputCooldown > 0) _inputCooldown -= (float)delta;
        if (Player == null || Player.CurrentState != PlayerState.BuildMode) return;

        Vector3 playerPos = Player.GlobalPosition;

        // Handle Replacement logic
        if (_replacingIndex != -1)
        {
            _points[_replacingIndex] = playerPos;
            _markers[_replacingIndex].GlobalPosition = playerPos + new Vector3(0, 0.1f, 0);
            UpdateLines();

            if (Input.IsActionJustPressed("ui_accept") || (Input.IsKeyPressed(Key.Space) && _inputCooldown <= 0))
            {
                _replacingIndex = -1;
                _inputCooldown = 0.3f;
            }
            return;
        }

        // Proximity detection for existing markers
        _closestMarkerIndex = -1;
        float minDist = 2.0f;
        for (int i = 0; i < _points.Count; i++)
        {
            float d = playerPos.DistanceTo(_points[i]);
            if (d < minDist)
            {
                minDist = d;
                _closestMarkerIndex = i;
            }

            // Visual feedback for proximity
            var mesh = _markers[i].GetChild<MeshInstance3D>(0);
            var mat = (StandardMaterial3D)mesh.MaterialOverride;
            mat.AlbedoColor = (i == _closestMarkerIndex) ? Colors.Cyan : Colors.Yellow;
        }

        // Proximity detection for existing baked terrain
        _closestTerrain = null;
        if (_closestMarkerIndex == -1 && _replacingIndex == -1)
        {
            var terrains = GetTree().GetNodesInGroup("surveyed_terrain");
            float minTerrainDist = 5.0f;
            foreach (Node t in terrains)
            {
                if (t is SurveyedTerrain st)
                {
                    foreach (var p in st.Points)
                    {
                        float d = playerPos.DistanceTo(p);
                        if (d < minTerrainDist)
                        {
                            minTerrainDist = d;
                            _closestTerrain = st;
                        }
                    }
                }
            }
        }

        // Update Prompt based on context (Center Prompt for temporary state/editing)
        if (_replacingIndex != -1)
        {
            _swingSystem.SetPrompt(true, "REPLACING POINT: SPACEBAR TO SET");
        }
        else if (_closestTerrain != null)
        {
            _swingSystem.SetPrompt(true, "E: EDIT TERRAIN | R: COPY | DEL: REMOVE");
        }
        else
        {
            _swingSystem.SetPrompt(false);
        }

        // Handle Inputs
        if (_closestTerrain != null)
        {
            if (Input.IsKeyPressed(Key.E) && _inputCooldown <= 0)
            {
                EditTerrain(_closestTerrain);
                _inputCooldown = 0.5f;
            }
            else if (Input.IsKeyPressed(Key.R) && _inputCooldown <= 0)
            {
                CopyTerrain(_closestTerrain);
                _inputCooldown = 0.5f;
            }
            else if (Input.IsKeyPressed(Key.Delete) && _inputCooldown <= 0)
            {
                _closestTerrain.QueueFree();
                _inputCooldown = 0.5f;
            }
        }

        // Tool-specific behavior
        if (_hud != null && _hud.CurrentTool != MainHUDController.BuildTool.Survey)
        {
            // If not in survey tool, don't allow terrain point management
            return;
        }

        if (_closestMarkerIndex != -1 && (Player == null || Player.SelectedObject == null))
        {
            if (Input.IsKeyPressed(Key.X) && _inputCooldown <= 0) // Delete
            {
                RemovePoint(_closestMarkerIndex);
                _inputCooldown = 0.3f;
            }
            else if (Input.IsKeyPressed(Key.C) && _inputCooldown <= 0) // Replace / Move
            {
                _replacingIndex = _closestMarkerIndex;
                _inputCooldown = 0.3f;
            }
        }
        else
        {
            if (Input.IsActionJustPressed("ui_accept") || (Input.IsKeyPressed(Key.Space) && _inputCooldown <= 0))
            {
                AddPoint(playerPos);
                _inputCooldown = 0.3f;
            }
        }

        if (Input.IsKeyPressed(Key.T) && _inputCooldown <= 0)
        {
            ClearSurvey();
            _inputCooldown = 0.3f;
        }

        if (_points.Count > 0)
        {
            UpdateLines();
        }
    }

    private void RemovePoint(int index)
    {
        _points.RemoveAt(index);
        _markers[index].QueueFree();
        _markers.RemoveAt(index);
        UpdateMarkerLabels();
        UpdateLines();
    }

    public void ClearSurvey()
    {
        _points.Clear();
        foreach (var m in _markers) m.QueueFree();
        _markers.Clear();
        _lineMesh.ClearSurfaces();
        if (_previewMeshInstance != null) _previewMeshInstance.QueueFree();
        _previewMeshInstance = null;
        IsPickingTerrain = false;
        CurrentElevation = 0.0f;
    }

    public void SetPreviewTerrain(int terrainType)
    {
        if (terrainType < 0) terrainType = 0;
        _lastSelectedType = terrainType;
        if (_points.Count < 3) return;

        if (_previewMeshInstance == null)
        {
            _previewMeshInstance = new CsgPolygon3D();
            _previewMeshInstance.Name = "SurveyPreview";
            _previewMeshInstance.Mode = CsgPolygon3D.ModeEnum.Depth;
            _previewMeshInstance.RotationDegrees = new Vector3(90, 0, 0);
            AddChild(_previewMeshInstance);
        }

        IsPickingTerrain = true;
        UpdateLines();

        Vector3 centroid = Vector3.Zero;
        foreach (var p in _points) centroid += p;
        centroid /= _points.Count;

        Vector2[] poly = new Vector2[_points.Count];
        for (int i = 0; i < _points.Count; i++)
        {
            poly[i] = new Vector2(_points[i].X - centroid.X, _points[i].Z - centroid.Z);
        }

        _previewMeshInstance.Polygon = poly;
        _previewMeshInstance.Show();

        if (CurrentElevation >= 0)
        {
            _previewMeshInstance.Depth = 0.1f + CurrentElevation;
            _previewMeshInstance.GlobalPosition = new Vector3(centroid.X, 0.05f, centroid.Z);
        }
        else
        {
            float depth = Mathf.Abs(CurrentElevation);
            _previewMeshInstance.Depth = depth;
            _previewMeshInstance.GlobalPosition = new Vector3(centroid.X, 0.05f - depth, centroid.Z);
        }

        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.AlbedoColor = new Color(1, 1, 1, 0.4f);

        switch (terrainType)
        {
            case 0: mat.AlbedoColor = new Color(0.1f, 0.4f, 0.1f, 0.6f); break; // Fairway
            case 1: mat.AlbedoColor = new Color(0.05f, 0.2f, 0.05f, 0.6f); break; // Rough
            case 2: mat.AlbedoColor = new Color(0.02f, 0.15f, 0.02f, 0.6f); break; // Deep Rough
            case 3: mat.AlbedoColor = new Color(0, 1, 0, 0.6f); break; // Green
            case 4: mat.AlbedoColor = new Color(1, 0.9f, 0.6f, 0.6f); break; // Sand
            case 5: mat.AlbedoColor = new Color(0, 0.4f, 1, 0.6f); break; // Water
            default: mat.AlbedoColor = new Color(1, 1, 1, 0.6f); break;
        }

        _previewMeshInstance.MaterialOverride = mat;
    }

    public void BakeTerrain(int terrainType)
    {
        if (terrainType < 0) terrainType = 0;
        if (_points.Count < 3) return;

        var heightmap = GetTree().CurrentScene.GetNodeOrNull<HeightmapTerrain>("HeightmapTerrain");
        if (heightmap == null)
        {
            var terrains = GetTree().GetNodesInGroup("terrain");
            if (terrains.Count > 0 && terrains[0] is HeightmapTerrain)
            {
                heightmap = (HeightmapTerrain)terrains[0];
            }
        }

        if (heightmap != null)
        {
            float heightDelta = CurrentElevation;
            heightmap.DeformArea(_points.ToArray(), heightDelta, terrainType);
            CurrentElevation = 0.0f;
            ClearSurvey();
            return;
        }

        SetupCSGRoot();

        var bakedNode = new SurveyedTerrain();
        bakedNode.Name = $"Terrain_{terrainType}_{Time.GetTicksMsec()}";
        bakedNode.Points = _points.ToArray();
        bakedNode.TerrainType = terrainType;

        Vector3 centroid = Vector3.Zero;
        foreach (var p in _points) centroid += p;
        centroid /= _points.Count;

        Vector2[] localPoly = new Vector2[_points.Count];
        for (int i = 0; i < _points.Count; i++)
            localPoly[i] = new Vector2(_points[i].X - centroid.X, _points[i].Z - centroid.Z);

        bakedNode.Polygon = localPoly;
        bakedNode.UseCollision = true;
        bakedNode.Mode = CsgPolygon3D.ModeEnum.Depth;
        bakedNode.RotationDegrees = new Vector3(90, 0, 0);
        bakedNode.UseCollision = false;

        var mat = new StandardMaterial3D();
        switch (terrainType)
        {
            case 0: mat.AlbedoColor = new Color(0.2f, 0.5f, 0.2f); break;
            case 1: mat.AlbedoColor = new Color(0.15f, 0.3f, 0.15f); break;
            case 2: mat.AlbedoColor = new Color(0.08f, 0.2f, 0.08f); break;
            case 3: mat.AlbedoColor = new Color(0.1f, 0.6f, 0.1f); break;
            case 4: mat.AlbedoColor = new Color(0.9f, 0.8f, 0.5f); break;
            case 5: mat.AlbedoColor = new Color(0.1f, 0.3f, 0.8f); break;
        }
        bakedNode.MaterialOverride = mat;

        if (CurrentElevation >= 0)
        {
            bakedNode.Operation = CsgShape3D.OperationEnum.Union;
            bakedNode.Depth = 0.1f + CurrentElevation;
            _csgRoot.AddChild(bakedNode);
            bakedNode.GlobalPosition = new Vector3(centroid.X, 0.1f, centroid.Z);
        }
        else
        {
            float depth = Mathf.Abs(CurrentElevation);
            bakedNode.Operation = CsgShape3D.OperationEnum.Subtraction;
            bakedNode.Depth = depth;
            _csgRoot.AddChild(bakedNode);
            bakedNode.GlobalPosition = new Vector3(centroid.X, 0.1f - depth, centroid.Z);

            if (terrainType == 4 || terrainType == 5)
            {
                var filler = new SurveyedTerrain();
                filler.Name = $"{bakedNode.Name}_Filler";
                filler.Points = _points.ToArray();
                filler.TerrainType = terrainType;
                filler.Polygon = localPoly;
                filler.Operation = CsgShape3D.OperationEnum.Union;
                filler.Mode = CsgPolygon3D.ModeEnum.Depth;
                filler.RotationDegrees = new Vector3(90, 0, 0);
                filler.UseCollision = false;

                float holeDepth = depth;
                float fillHeight = holeDepth * (_fillPercentage / 100.0f);
                float fillerBottomY = 0.1f - holeDepth;

                filler.Depth = fillHeight;
                filler.MaterialOverride = mat;
                _csgRoot.AddChild(filler);
                filler.GlobalPosition = new Vector3(centroid.X, fillerBottomY, centroid.Z);
            }
        }

        if (_csgRoot != null)
        {
            _csgRoot.UseCollision = false;
            _csgRoot.UseCollision = true;
        }

        CurrentElevation = 0.0f;
        ClearSurvey();
    }

    private void EditTerrain(SurveyedTerrain terrain)
    {
        ClearSurvey();
        foreach (var p in terrain.Points) AddPoint(p);
        terrain.QueueFree();
    }

    private void CopyTerrain(SurveyedTerrain terrain)
    {
        ClearSurvey();
        Vector3 centroid = Vector3.Zero;
        foreach (var p in terrain.Points) centroid += p;
        centroid /= terrain.Points.Length;

        Vector3 offset = Player.GlobalPosition - centroid;
        foreach (var p in terrain.Points) AddPoint(p + offset);
    }

    private void SetupCSGRoot()
    {
        var world = GetTree().CurrentScene;
        if (world == null) world = GetTree().Root.GetChild(0);

        _csgRoot = world.GetNodeOrNull<CsgCombiner3D>("TerrainCombiner");

        if (_csgRoot == null)
        {
            _csgRoot = new CsgCombiner3D();
            _csgRoot.Name = "TerrainCombiner";
            _csgRoot.UseCollision = true;
            world.AddChild(_csgRoot);

            var bedrock = new StaticBody3D();
            bedrock.Name = "Bedrock";
            var meshNode = new MeshInstance3D();
            var bMesh = new BoxMesh();
            bMesh.Size = new Vector3(2000, 5, 2000);
            meshNode.Mesh = bMesh;
            meshNode.Position = new Vector3(0, -5.0f, 0);
            var bMat = new StandardMaterial3D();
            bMat.AlbedoColor = new Color(0.15f, 0.1f, 0.05f);
            meshNode.MaterialOverride = bMat;
            bedrock.AddChild(meshNode);
            world.AddChild(bedrock);
        }

        string[] grounds = { "Fairway", "RoughLeft", "RoughRight", "TeeBox" };
        foreach (var gName in grounds)
        {
            var g = world.GetNodeOrNull<CsgShape3D>(gName);
            if (g != null)
            {
                if (g is CsgBox3D box)
                {
                    float originalYSize = box.Size.Y;
                    float newYSize = 20.0f;
                    if (originalYSize < 5.0f)
                    {
                        box.Size = new Vector3(box.Size.X, newYSize, box.Size.Z);
                        float shift = (newYSize - originalYSize) / 2.0f;
                        box.Position -= new Vector3(0, shift, 0);
                    }
                }
                g.Reparent(_csgRoot, true);
                g.UseCollision = false;
            }
        }
    }
}
