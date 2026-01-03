using Godot;
using System.Collections.Generic;

public partial class HeightmapTerrain : StaticBody3D
{
	[Export] public int GridWidth = 200;  // X axis
	[Export] public int GridDepth = 200;  // Z axis
	[Export] public float CellSize = 1.0f; // Size of each quad
	[Export] public Material TerrainMaterial;

	// Data
	private float[,] _heightData;
	private int[,] _terrainTypeData;

	public void SetData(int x, int z, float height, int type)
	{
		if (x >= 0 && x <= GridWidth && z >= 0 && z <= GridDepth)
		{
			_heightData[x, z] = height;
			_terrainTypeData[x, z] = type;
		}
	}

	public float[,] GetHeightData() => _heightData;
	public int[,] GetTerrainTypeData() => _terrainTypeData;

	// Components
	private MeshInstance3D _meshInstance;
	private CollisionShape3D _collisionShape;

	public override void _Ready()
	{
		InitializeData();
		// Try to find existing nodes to satisfy editor/user setup
		_meshInstance = GetNodeOrNull<MeshInstance3D>("TerrainMesh");
		_collisionShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");

		GD.Print($"HeightmapTerrain: Ready at Global Pos: {GlobalPosition}. Grid: {GridWidth}x{GridDepth}");

		CreateTerrainMesh();
		AddToGroup("terrain");
	}

	private void InitializeData()
	{
		_heightData = new float[GridWidth + 1, GridDepth + 1];
		_terrainTypeData = new int[GridWidth + 1, GridDepth + 1];

		// Default flat terrain
		for (int x = 0; x <= GridWidth; x++)
		{
			for (int z = 0; z <= GridDepth; z++)
			{
				_heightData[x, z] = 0.0f;
				_terrainTypeData[x, z] = 0; // 0 = Fairway
			}
		}
	}

	private void CreateTerrainMesh()
	{
		if (_meshInstance == null)
		{
			_meshInstance = new MeshInstance3D();
			_meshInstance.Name = "TerrainMesh";
			AddChild(_meshInstance);
		}

		UpdateMesh();
	}

	public void UpdateMesh()
	{
		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		// Simple Vertex Color Mapping for debug types
		// 0: Green (Fairway), 1: Dark Green (Rough), 4: Yellow (Sand), 5: Blue (Water)

		for (int z = 0; z <= GridDepth; z++)
		{
			for (int x = 0; x <= GridWidth; x++)
			{
				float h = _heightData[x, z];
				int type = _terrainTypeData[x, z];

				Color c = Colors.Green;
				if (type == 1) c = Colors.DarkGreen;
				if (type == 4) c = Colors.SandyBrown;
				if (type == 5) c = Colors.Blue;

				surfaceTool.SetColor(c);
				surfaceTool.SetUV(new Vector2(x * 0.1f, z * 0.1f));
				surfaceTool.AddVertex(new Vector3(x * CellSize, h, z * CellSize));
			}
		}

		// Create Indices (REVERSED winding for correct face orientation)
		for (int z = 0; z < GridDepth; z++)
		{
			for (int x = 0; x < GridWidth; x++)
			{
				int topLeft = z * (GridWidth + 1) + x;
				int topRight = topLeft + 1;
				int bottomLeft = (z + 1) * (GridWidth + 1) + x;
				int bottomRight = bottomLeft + 1;

				// Triangle 1 (reversed)
				surfaceTool.AddIndex(topLeft);
				surfaceTool.AddIndex(topRight);
				surfaceTool.AddIndex(bottomLeft);

				// Triangle 2 (reversed)
				surfaceTool.AddIndex(topRight);
				surfaceTool.AddIndex(bottomRight);
				surfaceTool.AddIndex(bottomLeft);
			}
		}

		surfaceTool.GenerateNormals();
		var mesh = surfaceTool.Commit();
		_meshInstance.Mesh = mesh;
		GD.Print($"HeightmapTerrain: Mesh created.");

		// Apply Material with Vertex Color support
		if (TerrainMaterial != null)
		{
			_meshInstance.MaterialOverride = TerrainMaterial;
		}
		else
		{
			var mat = new StandardMaterial3D();
			mat.VertexColorUseAsAlbedo = true;
			mat.AlbedoColor = Colors.White; // Ensure base color isn't black
			_meshInstance.MaterialOverride = mat;
		}

		UpdateCollision(mesh);
	}

	private void UpdateCollision(Mesh mesh)
	{
		// Don't create a new collision shape - use the one from the scene
		if (_collisionShape == null)
		{
			GD.Print("HeightmapTerrain: CollisionShape3D not found in scene, skipping collision update");
			return;
		}

		// Use trimesh collision generated directly from the mesh
		// This ensures collision matches the visual mesh exactly
		var trimeshShape = mesh.CreateTrimeshShape();

		if (trimeshShape == null)
		{
			GD.Print("HeightmapTerrain: Failed to create trimesh collision shape");
			return;
		}

		_collisionShape.Shape = trimeshShape;
		_collisionShape.Scale = Vector3.One;
		_collisionShape.Position = Vector3.Zero;

		GD.Print($"HeightmapTerrain: Updated Trimesh collision from mesh");
	}

	// Helper to deform an area
	public void DeformArea(Vector3[] globalPoints, float heightDelta, int terrainType)
	{
		GD.Print($"Heightmap: Deforming Area with {heightDelta}m. Type {terrainType}");

		// Convert global points to local space 2D polygon
		Vector2[] localPoly = new Vector2[globalPoints.Length];

		int minX = GridWidth, maxX = 0, minZ = GridDepth, maxZ = 0;

		for (int i = 0; i < globalPoints.Length; i++)
		{
			Vector3 local3D = ToLocal(globalPoints[i]);
			localPoly[i] = new Vector2(local3D.X, local3D.Z);

			// Calculate bounds
			int gx = Mathf.Clamp(Mathf.RoundToInt(local3D.X / CellSize), 0, GridWidth);
			int gz = Mathf.Clamp(Mathf.RoundToInt(local3D.Z / CellSize), 0, GridDepth);

			if (gx < minX) minX = gx;
			if (gx > maxX) maxX = gx;
			if (gz < minZ) minZ = gz;
			if (gz > maxZ) maxZ = gz;
		}

		// Expand padding for safety
		minX = Mathf.Clamp(minX - 5, 0, GridWidth);
		maxX = Mathf.Clamp(maxX + 5, 0, GridWidth);
		minZ = Mathf.Clamp(minZ - 5, 0, GridDepth);
		maxZ = Mathf.Clamp(maxZ + 5, 0, GridDepth);

		bool changed = false;

		for (int z = minZ; z <= maxZ; z++)
		{
			for (int x = minX; x <= maxX; x++)
			{
				// Grid point in Local Space
				Vector2 gridPos = new Vector2(x * CellSize, z * CellSize);

				if (Geometry2D.IsPointInPolygon(gridPos, localPoly))
				{
					_heightData[x, z] += heightDelta;
					_terrainTypeData[x, z] = terrainType;
					changed = true;
				}
			}
		}

		if (changed)
		{
			UpdateMesh();

			// Create fill mesh for sand (4) or water (5) hazards if it's a hole
			if (heightDelta < 0 && (terrainType == 4 || terrainType == 5))
			{
				CreateFillMesh(globalPoints, heightDelta, terrainType);
			}
		}
	}

	private void CreateFillMesh(Vector3[] globalPoints, float heightDelta, int terrainType)
	{
		// Calculate fill height (80% by default)
		float fillPercentage = 0.8f;
		float holeDepth = Mathf.Abs(heightDelta);
		float fillHeight = heightDelta + (holeDepth * fillPercentage);

		// Convert global points to local 2D polygon
		Vector2[] localPoly2D = new Vector2[globalPoints.Length];
		Vector3 centroid = Vector3.Zero;

		for (int i = 0; i < globalPoints.Length; i++)
		{
			centroid += globalPoints[i];
			Vector3 local3D = ToLocal(globalPoints[i]);
			localPoly2D[i] = new Vector2(local3D.X, local3D.Z);
		}
		centroid /= globalPoints.Length;

		// Create a flat mesh at the fill height
		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		// Triangulate the polygon using ear clipping (simple fan triangulation for now)
		Vector3 localCentroid = ToLocal(centroid);

		// Add center vertex
		Color fillColor = terrainType == 4 ? Colors.SandyBrown : new Color(0.2f, 0.4f, 0.8f, 0.7f);
		surfaceTool.SetColor(fillColor);
		surfaceTool.AddVertex(new Vector3(localCentroid.X, fillHeight, localCentroid.Z));

		// Add perimeter vertices
		for (int i = 0; i < localPoly2D.Length; i++)
		{
			surfaceTool.SetColor(fillColor);
			surfaceTool.AddVertex(new Vector3(localPoly2D[i].X, fillHeight, localPoly2D[i].Y));
		}

		// Create triangles (fan from center)
		for (int i = 0; i < localPoly2D.Length; i++)
		{
			int next = (i + 1) % localPoly2D.Length;
			surfaceTool.AddIndex(0); // Center
			surfaceTool.AddIndex(i + 1);
			surfaceTool.AddIndex(next + 1);
		}

		surfaceTool.GenerateNormals();
		var fillMesh = surfaceTool.Commit();

		// Apply material
		var mat = new StandardMaterial3D();
		mat.VertexColorUseAsAlbedo = true;

		if (terrainType == 5) // Water
		{
			mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			mat.AlbedoColor = new Color(0.2f, 0.4f, 0.8f, 0.7f);

			// Water is visual only - no collision
			var fillInstance = new MeshInstance3D();
			fillInstance.Mesh = fillMesh;
			fillInstance.Name = $"Fill_Water_{Time.GetTicksMsec()}";
			fillInstance.MaterialOverride = mat;
			AddChild(fillInstance);

			GD.Print($"Created water fill mesh at height {fillHeight}");
		}
		else // Sand (type 4)
		{
			mat.AlbedoColor = Colors.SandyBrown;

			// Sand has collision so player sinks into it
			var sandBody = new StaticBody3D();
			sandBody.Name = $"Fill_Sand_{Time.GetTicksMsec()}";
			sandBody.CollisionLayer = 2; // Same as terrain
			sandBody.CollisionMask = 0;

			// Add mesh instance
			var fillInstance = new MeshInstance3D();
			fillInstance.Mesh = fillMesh;
			fillInstance.MaterialOverride = mat;
			sandBody.AddChild(fillInstance);

			// Add collision shape
			var collisionShape = new CollisionShape3D();
			var trimeshShape = fillMesh.CreateTrimeshShape();
			collisionShape.Shape = trimeshShape;
			sandBody.AddChild(collisionShape);

			AddChild(sandBody);

			GD.Print($"Created sand fill mesh with collision at height {fillHeight}");
		}
	}
}
