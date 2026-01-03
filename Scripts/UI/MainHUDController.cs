using Godot;
using System;
using System.Collections.Generic;

public partial class MainHUDController : CanvasLayer
{
	[Export] public NodePath SwingSystemPath;
	[Export] public NodePath PlayerPath;

	private SwingSystem _swingSystem;
	private PlayerController _player;

	private Label _modeLabel;
	private Label _modeHint;
	private Label _buildHint1;
	private Label _buildHint2;
	private Label _buildHint3;
	private Label _buildHint4;
	private Label _buildHint5;
	private Label _promptLabel;

	private Control _golfHUD;
	private Control _buildHUD;
	private Control _walkHUD;

	private Control _toolsPanel;
	private Control _objectGallery;
	private HBoxContainer _categoryContainer;
	private GridContainer _objectGrid;
	private Button _galleryToggleBtn;
	private Control _galleryContent;

	public enum BuildTool { Selection, Survey, NewObject }
	private BuildTool _currentTool = BuildTool.Selection;
	public BuildTool CurrentTool => _currentTool;

	private struct ObjectAsset
	{
		public string Name;
		public string Path;
		public string Category;
	}
	private List<ObjectAsset> _allAssets = new List<ObjectAsset>();
	private string _currentCategory = "Trees";

	public override void _Ready()
	{
		_swingSystem = GetNodeOrNull<SwingSystem>(SwingSystemPath);
		_player = GetNodeOrNull<PlayerController>(PlayerPath);

		// Fallback for player
		if (_player == null)
			_player = GetTree().CurrentScene.FindChild("PlayerPlaceholder", true, false) as PlayerController;

		_modeLabel = GetNode<Label>("ModeLabel");

		var hints = GetNode<VBoxContainer>("BottomLeftHints");
		_modeHint = hints.GetNode<Label>("ModeHint");
		_buildHint1 = hints.GetNode<Label>("BuildHint1");
		_buildHint2 = hints.GetNode<Label>("BuildHint2");
		_buildHint3 = hints.GetNode<Label>("BuildHint3");
		_buildHint4 = hints.GetNode<Label>("BuildHint4");

		// Dynamically add 5th hint
		_buildHint5 = (Label)_buildHint4.Duplicate();
		hints.AddChild(_buildHint5);

		_promptLabel = GetNode<Label>("PromptContainer/PromptLabel");

		_golfHUD = GetNode<Control>("GolfHUD");
		_buildHUD = GetNode<Control>("BuildHUD");
		_walkHUD = GetNode<Control>("WalkHUD");

		_toolsPanel = GetNode<Control>("ToolsPanel");
		_objectGallery = GetNode<Control>("ObjectGallery");
		_galleryContent = GetNode<Control>("ObjectGallery/VBox");
		_objectGrid = GetNode<GridContainer>("ObjectGallery/VBox/Scroll/Grid");

		// Setup Minimize/Expand Button
		_galleryToggleBtn = new Button();
		_galleryToggleBtn.Text = "▶";
		_galleryToggleBtn.CustomMinimumSize = new Vector2(40, 40);
		_galleryToggleBtn.Size = new Vector2(40, 40);
		_galleryToggleBtn.Position = _objectGallery.Position; // Match gallery position
		_galleryToggleBtn.Pressed += () => SetGalleryExpanded(true);
		AddChild(_galleryToggleBtn); // Add to HUD directly, not inside gallery

		// Setup Category Container
		_categoryContainer = new HBoxContainer();
		_categoryContainer.Alignment = BoxContainer.AlignmentMode.Center;
		GetNode<VBoxContainer>("ObjectGallery/VBox").AddChild(_categoryContainer);
		GetNode<VBoxContainer>("ObjectGallery/VBox").MoveChild(_categoryContainer, 1); // Below Title

		// Connect Tool Buttons
		GetNode<Button>("ToolsPanel/VBox/SelectionBtn").Pressed += () => SetBuildTool(BuildTool.Selection);
		GetNode<Button>("ToolsPanel/VBox/SurveyBtn").Pressed += () => SetBuildTool(BuildTool.Survey);
		GetNode<Button>("ToolsPanel/VBox/NewObjectBtn").Pressed += () => SetBuildTool(BuildTool.NewObject);

		ScanAssets();
		CreateCategoryButtons();

		if (_swingSystem != null)
		{
			_swingSystem.Connect(SwingSystem.SignalName.PromptChanged, new Callable(this, MethodName.OnPromptChanged));
		}

		// Initial update
		UpdateHUDForMode(PlayerState.WalkMode);
	}

	public override void _Process(double delta)
	{
		if (_player != null)
		{
			UpdateHUDForMode(_player.CurrentState);
		}
	}

	private PlayerState _lastState = (PlayerState)(-1);
	private InteractableObject _lastSelectedObject = null;

	private void UpdateHUDForMode(PlayerState state)
	{
		var currentSelected = _player?.SelectedObject;
		bool stateChanged = (state != _lastState);
		bool selectionChanged = (state == PlayerState.BuildMode && currentSelected != _lastSelectedObject);

		if (!stateChanged && !selectionChanged) return;

		_lastState = state;
		_lastSelectedObject = currentSelected;

		string modeText = state.ToString().ToUpper().Replace("MODE", " MODE");
		if (state == PlayerState.BuildMode || state == PlayerState.PlacingObject)
		{
			modeText += $" - {_currentTool.ToString().ToUpper().Replace("NEWOBJECT", "NEW OBJECT")}";
		}
		_modeLabel.Text = modeText;

		// Toggle sub-HUDs
		_golfHUD.Visible = (state == PlayerState.GolfMode);
		_buildHUD.Visible = (state == PlayerState.BuildMode && _currentTool == BuildTool.Survey);
		_walkHUD.Visible = (state == PlayerState.WalkMode);

		_toolsPanel.Visible = (state == PlayerState.BuildMode || state == PlayerState.PlacingObject);
		if (state != PlayerState.BuildMode && state != PlayerState.PlacingObject)
		{
			_objectGallery.Visible = false;
			_galleryToggleBtn.Visible = false;
		}
		else if (_currentTool == BuildTool.NewObject)
		{
			// Visibility handled by SetBuildTool/SetGalleryExpanded state
			bool isExpanded = _objectGallery.Visible;
			SetGalleryExpanded(isExpanded);
		}

		// Update mode prompt hints
		if (state == PlayerState.WalkMode)
		{
			_modeHint.Visible = true;
			_modeHint.Text = "V: BUILD MODE";
			_buildHint1.Visible = false;
			_buildHint2.Visible = false;
			_buildHint3.Visible = false;
			_buildHint4.Visible = false;
			_buildHint5.Visible = false;
		}
		else if (state == PlayerState.BuildMode)
		{
			_modeHint.Visible = true;
			_modeHint.Text = "V: WALK MODE";

			// Customize hints based on tool
			if (_currentTool == BuildTool.Selection)
			{
				if (currentSelected != null)
				{
					_buildHint1.Visible = true; _buildHint1.Text = "LMB: Select / Drag Rotate";
					_buildHint2.Visible = true; _buildHint2.Text = "X: Delete | C: Move";
					_buildHint3.Visible = true; _buildHint3.Text = "Wheel: Rotate | Shift: Height";
					_buildHint4.Visible = true; _buildHint4.Text = "Ctrl+Wheel: Scale";
					_buildHint5.Visible = false;
				}
				else
				{
					// Hide all hints when nothing is selected
					_buildHint1.Visible = false;
					_buildHint2.Visible = false;
					_buildHint3.Visible = false;
					_buildHint4.Visible = false;
					_buildHint5.Visible = false;
				}
			}
			else // Turn/Survey
			{
				_buildHint1.Visible = true; _buildHint1.Text = "Spacebar: Drop Point";
				_buildHint2.Visible = true; _buildHint2.Text = "X: Delete";
				_buildHint3.Visible = true; _buildHint3.Text = "C: Reposition";
				_buildHint4.Visible = false;
				_buildHint5.Visible = false;
			}
		}
		else if (state == PlayerState.PlacingObject)
		{
			_modeHint.Visible = true;
			_modeHint.Text = "PLACING OBJECT";
			_buildHint1.Visible = true;
			_buildHint1.Text = "LMB: Place Object";
			_buildHint2.Visible = true;
			_buildHint2.Text = "RMB: Cancel Placement";
			_buildHint3.Visible = true;
			_buildHint3.Text = "Wheel: Rotate";
			_buildHint4.Visible = true;
			_buildHint4.Text = "Shift+Wheel: Adjust Height";
			_buildHint5.Visible = true;
			_buildHint5.Text = "Ctrl+Wheel: Scale";
		}
		else
		{
			_modeHint.Visible = false;
			_buildHint1.Visible = false;
			_buildHint2.Visible = false;
			_buildHint3.Visible = false;
			_buildHint4.Visible = false;
			_buildHint5.Visible = false;
		}

		GD.Print($"MainHUD: Switched to {state}");
	}

	private void OnPromptChanged(bool visible, string message)
	{
		if (_promptLabel != null)
		{
			_promptLabel.Visible = visible;
			if (!string.IsNullOrEmpty(message)) _promptLabel.Text = message;
		}
	}

	public void SetBuildTool(BuildTool tool)
	{
		_currentTool = tool;

		if (tool == BuildTool.NewObject)
		{
			SetGalleryExpanded(true);
			PopulateGallery(_currentCategory);
		}
		else
		{
			_objectGallery.Visible = false;
			_galleryToggleBtn.Visible = false;
		}

		GD.Print($"Build Tool Switched to: {tool}");

		// Update hints based on tool
		_lastState = (PlayerState)(-1); // Force hint refresh
		UpdateHUDForMode(_player?.CurrentState ?? PlayerState.BuildMode);
	}

	private void SelectObjectToPlace(string objectId)
	{
		GD.Print($"Selected Object to Place: {objectId}");

		string scenePath = "";
		bool isDirectGltf = false;

		var asset = _allAssets.Find(a => a.Name == objectId);
		if (!string.IsNullOrEmpty(asset.Path))
		{
			scenePath = asset.Path;
			isDirectGltf = true;
		}
		else
		{
			switch (objectId)
			{
				case "DistanceSign": scenePath = "res://Scenes/Environment/DistanceMarker.tscn"; break;
				case "TeePin": scenePath = "res://Scenes/Environment/DistanceMarker.tscn"; break;
				case "Pin": scenePath = "res://Scenes/Environment/DistanceMarker.tscn"; break;
			}
		}

		if (string.IsNullOrEmpty(scenePath)) return;

		if (isDirectGltf)
		{
			var model = GD.Load<PackedScene>(scenePath).Instantiate();
			var obj = new InteractableObject();
			obj.Name = objectId;
			obj.ObjectName = objectId;
			obj.AddChild(model);

			// Add a simple collision if missing
			var staticBody = new StaticBody3D();
			var col = new CollisionShape3D();
			var sphere = new SphereShape3D();
			sphere.Radius = 1.0f;
			col.Shape = sphere;
			staticBody.AddChild(col);
			obj.AddChild(staticBody);

			if (_swingSystem != null && _swingSystem.ObjectPlacer != null)
			{
				_swingSystem.ObjectPlacer.SpawnAndPlace(obj);
			}
		}
		else
		{
			var scene = GD.Load<PackedScene>(scenePath);
			if (_swingSystem != null && _swingSystem.ObjectPlacer != null)
			{
				var obj = scene.Instantiate<InteractableObject>();
				obj.ObjectName = objectId;
				_swingSystem.ObjectPlacer.SpawnAndPlace(obj);
			}
		}

		// Minimize gallery after selection
		SetGalleryExpanded(false);
	}

	private void SetGalleryExpanded(bool expanded)
	{
		_objectGallery.Visible = expanded;
		_galleryToggleBtn.Visible = !expanded;
	}

	private void ScanAssets()
	{
		_allAssets.Clear();
		_allAssets.Add(new ObjectAsset { Name = "TeePin", Category = "Utility", Path = "" });
		_allAssets.Add(new ObjectAsset { Name = "Pin", Category = "Utility", Path = "" });
		_allAssets.Add(new ObjectAsset { Name = "DistanceSign", Category = "Utility", Path = "" });

		string path = "res://Assets/Textures/Objects/";
		using var dir = DirAccess.Open(path);
		if (dir != null)
		{
			dir.ListDirBegin();
			string fileName = dir.GetNext();
			while (fileName != "")
			{
				if (fileName.EndsWith(".gltf"))
				{
					string category = GetCategoryForFile(fileName);
					_allAssets.Add(new ObjectAsset
					{
						Name = fileName.Replace(".gltf", ""),
						Path = path + fileName,
						Category = category
					});
				}
				fileName = dir.GetNext();
			}
		}
		GD.Print($"MainHUD: Scanned {_allAssets.Count} assets.");
	}

	private string GetCategoryForFile(string name)
	{
		name = name.ToLower();
		if (name.Contains("tree") || name.Contains("pine")) return "Trees";
		if (name.Contains("rock") || name.Contains("pebble")) return "Rocks";
		if (name.Contains("flower") || name.Contains("bush") || name.Contains("grass") || name.Contains("fern") || name.Contains("clover") || name.Contains("plant")) return "Greenery";
		if (name.Contains("mushroom")) return "Mushrooms";
		return "Misc";
	}

	private void CreateCategoryButtons()
	{
		string[] categories = { "Utility", "Trees", "Greenery", "Rocks", "Mushrooms", "Misc" };
		foreach (var cat in categories)
		{
			var btn = new Button { Text = cat };
			btn.CustomMinimumSize = new Vector2(100, 40);
			btn.Pressed += () => PopulateGallery(cat);
			_categoryContainer.AddChild(btn);
		}
	}

	private void PopulateGallery(string category)
	{
		_currentCategory = category;
		foreach (Node child in _objectGrid.GetChildren()) child.QueueFree();

		var filtered = _allAssets.FindAll(a => a.Category == category);
		foreach (var asset in filtered)
		{
			var btn = new Button { Text = asset.Name };
			btn.CustomMinimumSize = new Vector2(150, 40);
			btn.Pressed += () => SelectObjectToPlace(asset.Name);
			_objectGrid.AddChild(btn);
		}
	}
}
