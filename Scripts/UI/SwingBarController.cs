using Godot;
using System;

public partial class SwingBarController : CanvasLayer
{
	[Export] public NodePath SwingSystemPath;
	[Export] public NodePath WindSystemPath;
	[Export] public NodePath CameraControllerPath;

	private SwingSystem _swingSystem;
	private WindSystem _windSystem;
	private CameraController _cameraController;

	private ProgressBar _powerBar;
	private ColorRect _accuracyMarker;
	private Control _spinMarker;

	// UI Buttons
	private Button _backBtn;
	private Button _resetBtn;
	private Button _toggleWindBtn;

	// Removed Camera Buttons per request
	// private Button _btnZoom;
	// private Button _btnLand;
	// private Button _btnFree;

	private Label _carryLabel;
	private Label _totalLabel;
	private Label _speedLabel;
	private SpinBox _powerOverrideSpin;

	// Next Shot UI
	private Button _btnNextShot;
	private Label _strokeLabel;

	// Wind UI
	private TextureRect _windArrow;
	private Label _windLabel;
	private SpinBox _windSpeedSpin;

	private ColorRect _lockedPowerLine;
	private ColorRect _lockedAccuracyLine;
	private float _maxSpeed = 0.0f;

	// Bar mapping constants (0-100 Loop)
	private const float BAR_MIN = 0.0f;
	private const float BAR_MAX = 100.0f;
	private const float BAR_RANGE = BAR_MAX - BAR_MIN;

	public override void _Ready()
	{
		_swingSystem = GetNode<SwingSystem>(SwingSystemPath);
		if (WindSystemPath != null) _windSystem = GetNode<WindSystem>(WindSystemPath);
		if (CameraControllerPath != null) _cameraController = GetNode<CameraController>(CameraControllerPath);

		_powerBar = GetNode<ProgressBar>("SwingContainer/PowerBar");
		_accuracyMarker = GetNode<ColorRect>("SwingContainer/AccuracyMarker");

		_lockedPowerLine = GetNode<ColorRect>("SwingContainer/LockedPowerLine");
		_lockedAccuracyLine = GetNode<ColorRect>("SwingContainer/LockedAccuracyLine");

		_spinMarker = GetNode<Control>("SpinSelection/SpinMarker");

		// Wind UI
		_windArrow = GetNode<TextureRect>("WindContainer/WindArrow");
		_windLabel = GetNode<Label>("WindContainer/WindLabel");
		_windSpeedSpin = GetNode<SpinBox>("WindContainer/WindSpeedSpin");

		// Buttons
		_backBtn = GetNode<Button>("StatsPanel/BackBtn");
		_resetBtn = GetNode<Button>("StatsPanel/ResetBtn");
		_toggleWindBtn = GetNode<Button>("StatsPanel/ToggleWindBtn");

		_btnNextShot = GetNode<Button>("SwingContainer/BtnNextShot");

		_carryLabel = GetNode<Label>("StatsPanel/DistanceLabel");
		_totalLabel = GetNode<Label>("StatsPanel/TotalLabel");
		_speedLabel = GetNode<Label>("StatsPanel/SpeedLabel");
		_powerOverrideSpin = GetNode<SpinBox>("StatsPanel/PowerOverrideSpin");

		_strokeLabel = GetNode<Label>("StatsPanel/StrokeLabel");

		// Hide confusing base attributes for now
		GetNode<Label>("StatsPanel/PowerLabel").Visible = false;
		GetNode<Label>("StatsPanel/ControlLabel").Visible = false;

		// Connect Signals
		_swingSystem.Connect(SwingSystem.SignalName.SwingValuesUpdated, new Callable(this, MethodName.OnSwingValuesUpdated));
		_swingSystem.Connect(SwingSystem.SignalName.ShotDistanceUpdated, new Callable(this, MethodName.OnShotDistanceUpdated));
		_swingSystem.Connect(SwingSystem.SignalName.ShotResult, new Callable(this, MethodName.OnShotResult));
		_swingSystem.Connect(SwingSystem.SignalName.StrokeUpdated, new Callable(this, MethodName.OnStrokeUpdated));

		if (_windSystem != null)
		{
			_windSystem.WindChanged += OnWindChanged;
			OnWindChanged(_windSystem.WindDirection, _windSystem.WindSpeedMph); // Sync
			_toggleWindBtn.Text = _windSystem.IsWindEnabled ? "Wind: ON" : "Wind: OFF";
			GetNode<Control>("WindContainer").Visible = _windSystem.IsWindEnabled;
		}

		_backBtn.Pressed += OnBackPressed;
		_resetBtn.Pressed += OnResetPressed;
		_toggleWindBtn.Pressed += OnWindTogglePressed;
		_btnNextShot.Pressed += OnNextShotPressed;

		if (_windSpeedSpin != null)
		{
			_windSpeedSpin.ValueChanged += (val) => _windSystem?.SetWindSpeed((float)val);
			// Prevent container from grabbing focus
			_windSpeedSpin.FocusMode = Control.FocusModeEnum.None;
			// Allow text to be clicked
			_windSpeedSpin.GetLineEdit().FocusMode = Control.FocusModeEnum.Click;
		}

		if (_powerOverrideSpin != null)
		{
			_powerOverrideSpin.ValueChanged += (val) => _swingSystem?.SetPowerOverride((float)val);
			_powerOverrideSpin.Value = 5.0;
			_powerOverrideSpin.FocusMode = Control.FocusModeEnum.None;
			_powerOverrideSpin.GetLineEdit().FocusMode = Control.FocusModeEnum.Click;
		}

		// Connect Directional Buttons
		string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
		foreach (var dir in dirs)
		{
			var btn = GetNode<Button>($"WindContainer/WindDirGrid/Btn{dir}");
			btn.Pressed += () => OnWindDirPressed(dir);
		}
	}

	public override void _Input(InputEvent @event)
	{
		// 0. Aggressive Focus Release logic
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			bool isMoveKey = keyEvent.Keycode == Key.W || keyEvent.Keycode == Key.A ||
							 keyEvent.Keycode == Key.S || keyEvent.Keycode == Key.D ||
							 keyEvent.Keycode == Key.Escape || keyEvent.Keycode == Key.Enter;

			if (isMoveKey)
			{
				if (_windSpeedSpin != null && _windSpeedSpin.GetLineEdit().HasFocus())
					_windSpeedSpin.GetLineEdit().ReleaseFocus();

				if (_powerOverrideSpin != null && _powerOverrideSpin.GetLineEdit().HasFocus())
					_powerOverrideSpin.GetLineEdit().ReleaseFocus();
			}
		}

		// 1. Filter out UI clicks
		if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
		{
			if (GetNode<Control>("SpinSelection").GetGlobalRect().HasPoint(mouseBtn.Position))
			{
				UpdateSpinIntent(mouseBtn.Position);
				return;
			}

			bool isUIClick = _backBtn.GetGlobalRect().HasPoint(mouseBtn.Position) ||
							 _resetBtn.GetGlobalRect().HasPoint(mouseBtn.Position) ||
							 _toggleWindBtn.GetGlobalRect().HasPoint(mouseBtn.Position) ||
							 (_btnNextShot.Visible && _btnNextShot.GetGlobalRect().HasPoint(mouseBtn.Position)) ||
							 (_windSpeedSpin != null && _windSpeedSpin.GetGlobalRect().HasPoint(mouseBtn.Position)) ||
							 (_powerOverrideSpin != null && _powerOverrideSpin.GetGlobalRect().HasPoint(mouseBtn.Position));

			if (!isUIClick)
			{
				var grid = GetNode<GridContainer>("WindContainer/WindDirGrid");
				if (grid.GetGlobalRect().HasPoint(mouseBtn.Position)) isUIClick = true;
			}

			if (isUIClick) return;

			_swingSystem.HandleInput();
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("ui_accept", false))
		{
			_swingSystem.HandleInput();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnNextShotPressed()
	{
		_btnNextShot.Visible = false;
		_swingSystem.PrepareNextShot();
	}

	private void OnStrokeUpdated(int stroke)
	{
		_strokeLabel.Text = $"Stroke: {stroke}";
		// Ensure button resets if resetting fully
		if (stroke == 1) _btnNextShot.Visible = false;
	}

	private void OnWindDirPressed(string dir)
	{
		if (_windSystem == null) return;
		Vector3 direction = Vector3.Zero;
		switch (dir)
		{
			case "N": direction = new Vector3(0, 0, 1); break;
			case "S": direction = new Vector3(0, 0, -1); break;
			case "E": direction = new Vector3(-1, 0, 0); break;
			case "W": direction = new Vector3(1, 0, 0); break;
			case "NE": direction = new Vector3(-1, 0, 1).Normalized(); break;
			case "NW": direction = new Vector3(1, 0, 1).Normalized(); break;
			case "SE": direction = new Vector3(-1, 0, -1).Normalized(); break;
			case "SW": direction = new Vector3(1, 0, -1).Normalized(); break;
		}
		_windSystem.SetWindDirection(direction);
	}

	private void OnWindTogglePressed()
	{
		if (_windSystem == null) return;
		_windSystem.ToggleWind();
		_toggleWindBtn.Text = _windSystem.IsWindEnabled ? "Wind: ON" : "Wind: OFF";
		GetNode<Control>("WindContainer").Visible = _windSystem.IsWindEnabled;
	}

	private void OnWindChanged(Vector3 direction, float speedMph)
	{
		if (_windArrow == null || _windLabel == null) return;
		_windLabel.Text = $"{speedMph:F0} mph";
		if (_windSpeedSpin != null && Math.Abs(_windSpeedSpin.Value - speedMph) > 0.01f)
			_windSpeedSpin.Value = speedMph;
		_windArrow.Rotation = Mathf.Atan2(-direction.X, direction.Z);
	}

	private void OnShotResult(float power, float accuracy)
	{
		Label pLabel = GetNode<Label>("StatsPanel/PowerLabel");
		Label cLabel = GetNode<Label>("StatsPanel/ControlLabel");
		pLabel.Visible = true;
		cLabel.Visible = true;
		pLabel.Text = $"Shot Power: {power:F1}";
		cLabel.Text = $"Accuracy: {accuracy:F1}";
	}

	private void OnShotDistanceUpdated(float carry, float total)
	{
		if (carry == -2.0f)
		{
			if (total > _maxSpeed) _maxSpeed = total;
			_speedLabel.Text = $"Speed: {total:F1} m/s";
		}
		else if (carry >= 0)
		{
			_carryLabel.Text = $"Carry: {carry:F1}y";
			if (carry == 0)
			{
				_speedLabel.Text = "Speed: 0.0 m/s";
				_maxSpeed = 0.0f;
			}
		}

		// Only show Next Shot button if we have a valid accumulated distance > 0 (avoids showing on Reset)
		if (total > 0.01f && carry != -2.0f)
		{
			_totalLabel.Text = $"Total: {total:F1}y";
			_speedLabel.Text = $"Max Speed: {_maxSpeed:F1} m/s";
			_btnNextShot.Visible = true;
		}
		else if (total == 0.0f)
		{
			_totalLabel.Text = "Total: 0.0y";
		}
	}

	private void OnBackPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Menus/MainMenu.tscn");
	}

	private void OnResetPressed()
	{
		GetNode<Label>("StatsPanel/PowerLabel").Visible = false;
		GetNode<Label>("StatsPanel/ControlLabel").Visible = false;
		_lockedPowerLine.Visible = false;
		_lockedAccuracyLine.Visible = false;
		_btnNextShot.Visible = false;
		_swingSystem.ResetSwing();
	}

	private void OnSwingValuesUpdated(float currentBarValue, float lockedPower, float lockedAccuracy)
	{
		_powerBar.Value = currentBarValue;
		float parentWidth = _powerBar.Size.X;

		if (lockedAccuracy < 0)
		{
			float ghostRatio = (currentBarValue - BAR_MIN) / BAR_RANGE;
			_accuracyMarker.Position = new Vector2(ghostRatio * parentWidth - (_accuracyMarker.Size.X / 2.0f), _accuracyMarker.Position.Y);
			_accuracyMarker.Color = new Color(1, 1, 1, 0.5f);
			_accuracyMarker.Visible = true;
		}
		else
		{
			_accuracyMarker.Visible = false;
		}

		if (lockedPower >= 0)
		{
			_lockedPowerLine.Visible = true;
			float pRatio = (lockedPower - BAR_MIN) / BAR_RANGE;
			_lockedPowerLine.Position = new Vector2(pRatio * parentWidth - (_lockedPowerLine.Size.X / 2.0f), _lockedPowerLine.Position.Y);
			if (Math.Abs(lockedPower - 90.0f) < 1.0f) _lockedPowerLine.Color = Colors.Green;
			else _lockedPowerLine.Color = Colors.Yellow;
			GetNode<Label>("StatsPanel/PowerLabel").Visible = true;
			GetNode<Label>("StatsPanel/PowerLabel").Text = $"Power: {lockedPower:F0}";
		}
		else
		{
			_lockedPowerLine.Visible = false;
			GetNode<Label>("StatsPanel/PowerLabel").Visible = false;
		}

		if (lockedAccuracy >= 0)
		{
			_lockedAccuracyLine.Visible = true;
			float aRatio = (lockedAccuracy - BAR_MIN) / BAR_RANGE;
			_lockedAccuracyLine.Position = new Vector2(aRatio * parentWidth - (_lockedAccuracyLine.Size.X / 2.0f), _lockedAccuracyLine.Position.Y);
			if (Math.Abs(lockedAccuracy - 25.0f) <= 1.0f) _lockedAccuracyLine.Color = Colors.Green;
			else _lockedAccuracyLine.Color = Colors.Yellow;
			GetNode<Label>("StatsPanel/ControlLabel").Visible = true;
			GetNode<Label>("StatsPanel/ControlLabel").Text = $"Acc: {lockedAccuracy:F0}";
		}
		else
		{
			_lockedAccuracyLine.Visible = false;
		}

		float anger = _swingSystem.GetAnger();
		float angerRatio = anger / 100.0f;
		_powerBar.Modulate = new Color(1.0f, 1.0f - angerRatio * 0.5f, 1.0f - angerRatio * 0.5f);
	}

	private void UpdateSpinIntent(Vector2 globalPos)
	{
		Control spinContainer = GetNode<Control>("SpinSelection");
		if (spinContainer == null) return;

		Vector2 localPos = spinContainer.GetGlobalTransform().AffineInverse() * globalPos;
		Vector2 normalizedSpin = new Vector2(
			(localPos.X / spinContainer.Size.X) * 2.0f - 1.0f,
			(localPos.Y / spinContainer.Size.Y) * 2.0f - 1.0f
		);

		_spinMarker.Position = localPos - (_spinMarker.Size / 2.0f);
		_swingSystem.SetSpinIntent(normalizedSpin);
	}
}
