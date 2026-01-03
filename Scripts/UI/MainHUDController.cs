using Godot;
using System;

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
	private Label _promptLabel;

	private Control _golfHUD;
	private Control _buildHUD;
	private Control _walkHUD;

	private Control _toolsPanel;
	private Control _objectGallery;

	public enum BuildTool { Selection, Survey, NewObject }
	private BuildTool _currentTool = BuildTool.Selection;
	public BuildTool CurrentTool => _currentTool;

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

		_promptLabel = GetNode<Label>("PromptContainer/PromptLabel");

		_golfHUD = GetNode<Control>("GolfHUD");
		_buildHUD = GetNode<Control>("BuildHUD");
		_walkHUD = GetNode<Control>("WalkHUD");

		_toolsPanel = GetNode<Control>("ToolsPanel");
		_objectGallery = GetNode<Control>("ObjectGallery");

		// Connect Tool Buttons
		GetNode<Button>("ToolsPanel/VBox/SelectionBtn").Pressed += () => SetBuildTool(BuildTool.Selection);
		GetNode<Button>("ToolsPanel/VBox/SurveyBtn").Pressed += () => SetBuildTool(BuildTool.Survey);
		GetNode<Button>("ToolsPanel/VBox/NewObjectBtn").Pressed += () => SetBuildTool(BuildTool.NewObject);

		// Connect Object Buttons
		var signBtn = GetNodeOrNull<Button>("ObjectGallery/VBox/Scroll/Grid/SignBtn");
		if (signBtn != null) signBtn.Pressed += () => SelectObjectToPlace("DistanceSign");

		var teePinBtn = GetNodeOrNull<Button>("ObjectGallery/VBox/Scroll/Grid/TeePinBtn");
		if (teePinBtn != null) teePinBtn.Pressed += () => SelectObjectToPlace("TeePin");

		var pinBtn = GetNodeOrNull<Button>("ObjectGallery/VBox/Scroll/Grid/PinBtn");
		if (pinBtn != null) pinBtn.Pressed += () => SelectObjectToPlace("Pin");

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

	private void UpdateHUDForMode(PlayerState state)
	{
		if (state == _lastState) return;
		_lastState = state;

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
		}
		else if (_currentTool == BuildTool.NewObject)
		{
			_objectGallery.Visible = true;
		}

		// Update mode prompt hints
		if (state == PlayerState.WalkMode)
		{
			_modeHint.Visible = true;
			_modeHint.Text = "V: BUILD MODE";
			_buildHint1.Visible = false;
			_buildHint2.Visible = false;
			_buildHint3.Visible = false;
		}
		else if (state == PlayerState.BuildMode)
		{
			_modeHint.Visible = true;
			_modeHint.Text = "V: WALK MODE";
			_buildHint1.Visible = true;
			_buildHint1.Text = "Spacebar: Drop Point";
			_buildHint2.Visible = true;
			_buildHint2.Text = "X: Delete";
			_buildHint3.Visible = true;
			_buildHint3.Text = "C: Reposition";
			_buildHint4.Visible = false;
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
		}
		else
		{
			_modeHint.Visible = false;
			_buildHint1.Visible = false;
			_buildHint2.Visible = false;
			_buildHint3.Visible = false;
			_buildHint4.Visible = false;
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
		_objectGallery.Visible = (tool == BuildTool.NewObject);
		GD.Print($"Build Tool Switched to: {tool}");

		// Update hints based on tool
		_lastState = (PlayerState)(-1); // Force hint refresh
		UpdateHUDForMode(_player?.CurrentState ?? PlayerState.BuildMode);
	}

	private void SelectObjectToPlace(string objectId)
	{
		GD.Print($"Selected Object to Place: {objectId}");

		string scenePath = "";
		switch (objectId)
		{
			case "DistanceSign":
				scenePath = "res://Scenes/Environment/DistanceMarker.tscn";
				break;
			case "TeePin":
				scenePath = "res://Scenes/Environment/DistanceMarker.tscn"; // Placeholder
				break;
			case "Pin":
				scenePath = "res://Scenes/Environment/DistanceMarker.tscn"; // Placeholder
				break;
		}

		if (string.IsNullOrEmpty(scenePath)) return;

		var scene = GD.Load<PackedScene>(scenePath);
		if (_swingSystem != null && _swingSystem.ObjectPlacer != null)
		{
			var obj = scene.Instantiate<InteractableObject>();
			obj.ObjectName = objectId;
			_swingSystem.ObjectPlacer.SpawnAndPlace(obj);
		}
	}
}
