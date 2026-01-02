using Godot;
using System.Collections.Generic;

public partial class SurveyManager : Node3D
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
			var label = _markers[i].GetNode<Label3D>("Label3D");
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
		if (!IsPickingTerrain && Player != null && Player.CurrentState == PlayerState.Surveying && _replacingIndex == -1)
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
		GD.Print($"SurveyManager: Elevation modified to {CurrentElevation}");
		// Refresh preview if we are picking terrain
		SetPreviewTerrain(_lastSelectedType);
	}

	public void SetFillPercentage(float pct)
	{
		_fillPercentage = pct;
		GD.Print($"SurveyManager: Fill percentage set to {_fillPercentage}%");
		SetPreviewTerrain(_lastSelectedType);
	}

	public override void _Process(double delta)
	{
		if (_inputCooldown > 0) _inputCooldown -= (float)delta;
		if (Player == null || Player.CurrentState != PlayerState.Surveying) return;

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
		float minDist = 2.0f; // 2 meters detection
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
					// Basic distance check to centroid or nearest point
					// For now, check distance to the node's origin which is 0,0,0 usually but we can use st.GlobalPosition if we offset it
					// Better: check distance to points
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

		// Update Prompt based on context
		string promptPrefix = (_replacingIndex != -1) ? "REPLACING POINT: SPACE TO SET" : "SURVEY MODE: SPACE TO DROP POINT";
		if (_closestMarkerIndex != -1 && _replacingIndex == -1)
		{
			_swingSystem.SetPrompt(true, $"{promptPrefix} | X: DELETE | C: REPOSITION");
		}
		else if (_closestTerrain != null)
		{
			_swingSystem.SetPrompt(true, "EXISTING TERRAIN: E to EDIT | R to COPY | DEL to REMOVE");
		}
		else
		{
			_swingSystem.SetPrompt(true, promptPrefix);
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

		if (_closestMarkerIndex != -1)
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
		CurrentElevation = 0.0f; // Reset elevation on clear
	}

	public void SetPreviewTerrain(int terrainType)
	{
		if (terrainType < 0) terrainType = 0;
		GD.Print($"SurveyManager: SetPreviewTerrain called for type {terrainType}");
		_lastSelectedType = terrainType;
		if (_points.Count < 3) return;

		if (_previewMeshInstance == null)
		{
			_previewMeshInstance = new CsgPolygon3D();
			_previewMeshInstance.Name = "SurveyPreview";
			// Important: Use Depth mode and rotate to lay it flat
			// (90, 0, 0) maps Local Y to Global Z (flipped) but Extrusion is Down (-Y)
			_previewMeshInstance.Mode = CsgPolygon3D.ModeEnum.Depth;
			_previewMeshInstance.RotationDegrees = new Vector3(90, 0, 0);
			AddChild(_previewMeshInstance);
		}

		IsPickingTerrain = true;
		UpdateLines();

		// Calculate centroid and local polygon
		Vector3 centroid = Vector3.Zero;
		foreach (var p in _points) centroid += p;
		centroid /= _points.Count;

		Vector2[] poly = new Vector2[_points.Count];
		for (int i = 0; i < _points.Count; i++)
		{
			// Note: Map Global X -> Local X, Global Z -> Local Y
			poly[i] = new Vector2(_points[i].X - centroid.X, _points[i].Z - centroid.Z);
		}

		_previewMeshInstance.Polygon = poly;
		_previewMeshInstance.Show();

		// Preview Elevation
		if (CurrentElevation >= 0)
		{
			// Hill: Extrude UP from surface
			_previewMeshInstance.Depth = 0.1f + CurrentElevation;
			_previewMeshInstance.GlobalPosition = new Vector3(centroid.X, 0.05f, centroid.Z);
		}
		else
		{
			// Hole: Extrude UP from Bottom to Surface
			// Origin must be at (Surface - Depth)
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
		GD.Print($"SurveyManager: CSG Preview updated. Elev: {CurrentElevation}");
	}

	public void BakeTerrain(int terrainType)
	{
		if (terrainType < 0) terrainType = 0;
		GD.Print($"SurveyManager: BakeTerrain called for Type {terrainType}");
		if (_points.Count < 3) return;

		// CHECK FOR NEW HEIGHTMAP SYSTEM
		var heightmap = GetTree().CurrentScene.GetNodeOrNull<HeightmapTerrain>("HeightmapTerrain");
		if (heightmap == null)
		{
			// Try finding by group if name fails
			var terrains = GetTree().GetNodesInGroup("terrain");
			if (terrains.Count > 0 && terrains[0] is HeightmapTerrain)
			{
				heightmap = (HeightmapTerrain)terrains[0];
			}
		}

		if (heightmap != null)
		{
			// === NEW HEIGHTMAP LOGIC ===
			// Pass Global Points directly (Vector3[])
			// HeightmapTerrain handles the ToLocal conversion now

			// Calculate height delta from CurrentElevation
			float heightDelta = CurrentElevation;

			// Execute Deformation
			heightmap.DeformArea(_points.ToArray(), heightDelta, terrainType);

			GD.Print($"SurveyManager: Applied heightmap deformation. Elev: {CurrentElevation}");
			CurrentElevation = 0.0f;
			ClearSurvey();
			return;
		}

		// === FALLBACK TO OLD CSG LOGIC ===

		// Ensure we have a Csg root for digging and architecture
		SetupCSGRoot();

		var bakedNode = new SurveyedTerrain();
		bakedNode.Name = $"Terrain_{terrainType}_{Time.GetTicksMsec()}";
		bakedNode.Points = _points.ToArray();
		bakedNode.TerrainType = terrainType;

		// Calculate centroid and local polygon
		Vector3 centroid = Vector3.Zero;
		foreach (var p in _points) centroid += p;
		centroid /= _points.Count;

		Vector2[] localPoly = new Vector2[_points.Count];
		for (int i = 0; i < _points.Count; i++)
			localPoly[i] = new Vector2(_points[i].X - centroid.X, _points[i].Z - centroid.Z);

		bakedNode.Polygon = localPoly;
		bakedNode.UseCollision = true;
		// Important: Set Mode to Depth (same as preview)
		bakedNode.Mode = CsgPolygon3D.ModeEnum.Depth;
		bakedNode.RotationDegrees = new Vector3(90, 0, 0); // Consistent with preview (Extrudes Down)

		// CRITICAL: Do NOT set UseCollision = true on the operands!
		// The CSGCombiner3D handles the collision for the composite shape.
		// If this is true on a Subtraction node, it creates an invisible solid blocker.
		bakedNode.UseCollision = false;
		var mat = new StandardMaterial3D();
		switch (terrainType)
		{
			case 0: mat.AlbedoColor = new Color(0.2f, 0.5f, 0.2f); break; // Fairway
			case 1: mat.AlbedoColor = new Color(0.15f, 0.3f, 0.15f); break; // Rough
			case 2: mat.AlbedoColor = new Color(0.08f, 0.2f, 0.08f); break; // Deep Rough
			case 3: mat.AlbedoColor = new Color(0.1f, 0.6f, 0.1f); break; // Green
			case 4: mat.AlbedoColor = new Color(0.9f, 0.8f, 0.5f); break; // Sand
			case 5: mat.AlbedoColor = new Color(0.1f, 0.3f, 0.8f); break; // Water
		}
		bakedNode.MaterialOverride = mat;

		if (CurrentElevation >= 0)
		{
			// Raised Terrain - Additive (Extrudes UP)
			bakedNode.Operation = CsgShape3D.OperationEnum.Union;
			bakedNode.Depth = 0.1f + CurrentElevation;
			_csgRoot.AddChild(bakedNode);
			// Start at Surface (0.1), go UP
			bakedNode.GlobalPosition = new Vector3(centroid.X, 0.1f, centroid.Z);
		}
		else
		{
			// Lowered Terrain - Subtractive (Cutting a hole)
			// Extrudes UP. To cut from Surface down, we must positions it at (Surface - Depth).
			float depth = Mathf.Abs(CurrentElevation);
			bakedNode.Operation = CsgShape3D.OperationEnum.Subtraction;
			bakedNode.Depth = depth;
			_csgRoot.AddChild(bakedNode);
			bakedNode.GlobalPosition = new Vector3(centroid.X, 0.1f - depth, centroid.Z);

			// Filling Logic for Holes (Hazards)
			if (terrainType == 4 || terrainType == 5)
			{
				var filler = new SurveyedTerrain();
				filler.Name = $"{bakedNode.Name}_Filler";
				filler.Points = _points.ToArray();
				filler.TerrainType = terrainType;
				filler.Polygon = localPoly;
				filler.Operation = CsgShape3D.OperationEnum.Union;
				filler.Mode = CsgPolygon3D.ModeEnum.Depth; // Explicitly set mode
				filler.RotationDegrees = new Vector3(90, 0, 0);
				filler.UseCollision = false; // Let Combiner handle it

				// Position based on fill percentage
				// Pit goes from (0.1 - Depth) UP to 0.1.
				// Filler Bottom is same as Pit Bottom: (0.1 - Depth).
				// Filler Height is FillHeight.

				float holeDepth = depth;
				float fillHeight = holeDepth * (_fillPercentage / 100.0f);
				float fillerBottomY = 0.1f - holeDepth;

				filler.Depth = fillHeight;
				filler.MaterialOverride = mat;
				_csgRoot.AddChild(filler);
				filler.GlobalPosition = new Vector3(centroid.X, fillerBottomY, centroid.Z);
			}
		}

		GD.Print($"SurveyManager: Terrain baked at {CurrentElevation}m. Type {terrainType}");

		// Force physics update
		if (_csgRoot != null)
		{
			_csgRoot.UseCollision = false;
			_csgRoot.UseCollision = true;
		}

		CurrentElevation = 0.0f; // Reset for next shape
		ClearSurvey();
	}

	private void EditTerrain(SurveyedTerrain terrain)
	{
		ClearSurvey();
		foreach (var p in terrain.Points)
		{
			AddPoint(p);
		}
		terrain.QueueFree(); // Remove the old one while editing
	}

	private void CopyTerrain(SurveyedTerrain terrain)
	{
		// For simple "Copy", just load the points as new points but OFFSET to current player position?
		// Or just load them so you can see the shape and then move it?
		// Let's load the shape relative to its centroid and move it to the player.

		ClearSurvey();
		Vector3 centroid = Vector3.Zero;
		foreach (var p in terrain.Points) centroid += p;
		centroid /= terrain.Points.Length;

		Vector3 offset = Player.GlobalPosition - centroid;
		foreach (var p in terrain.Points)
		{
			AddPoint(p + offset);
		}
	}

	private void SetupCSGRoot()
	{
		// Use CurrentScene for safer root resolution
		var world = GetTree().CurrentScene;
		if (world == null)
		{
			// Fallback if CurrentScene isn't ready (unlikely in game)
			world = GetTree().Root.GetChild(0);
		}

		_csgRoot = world.GetNodeOrNull<CsgCombiner3D>("TerrainCombiner");

		if (_csgRoot == null)
		{
			GD.Print("SurveyManager: Creating new TerrainCombiner...");
			_csgRoot = new CsgCombiner3D();
			_csgRoot.Name = "TerrainCombiner";
			_csgRoot.UseCollision = true;
			world.AddChild(_csgRoot);

			// Add Bedrock Layer (Deep layer to prevent seeing void)
			var bedrock = new StaticBody3D();
			bedrock.Name = "Bedrock";
			var meshNode = new MeshInstance3D();
			var bMesh = new BoxMesh();
			bMesh.Size = new Vector3(2000, 5, 2000);
			meshNode.Mesh = bMesh;
			meshNode.Position = new Vector3(0, -5.0f, 0); // Position it below the top layer
			var bMat = new StandardMaterial3D();
			bMat.AlbedoColor = new Color(0.15f, 0.1f, 0.05f);
			meshNode.MaterialOverride = bMat;
			bedrock.AddChild(meshNode);
			world.AddChild(bedrock);
		}

		// Check for grounds to move (idempotent: if already moved, GetNodeOrNull won't find them at root)
		string[] grounds = { "Fairway", "RoughLeft", "RoughRight", "TeeBox" };
		foreach (var gName in grounds)
		{
			var g = world.GetNodeOrNull<CsgShape3D>(gName);
			if (g != null)
			{
				// Thicken ground to ensure we have a "floor" when digging
				if (g is CsgBox3D box)
				{
					float originalYSize = box.Size.Y;
					float newYSize = 20.0f; // Extra thick for safety
											// Only thicken if thin
					if (originalYSize < 5.0f)
					{
						box.Size = new Vector3(box.Size.X, newYSize, box.Size.Z);

						// Shift local position down by half the size difference to keep the TOP level
						// Center shifts down by (New - Old) / 2
						float shift = (newYSize - originalYSize) / 2.0f;
						box.Position -= new Vector3(0, shift, 0);
						GD.Print($"SurveyManager: Thickened {g.Name} to {newYSize}m, shifted down by {shift}m");
					}
				}
				g.Reparent(_csgRoot, true);
				g.UseCollision = false; // Explicitly disable child collision so Combiner takes over
				GD.Print($"SurveyManager: Moved {g.Name} into TerrainCombiner.");
			}
		}
	}
}
