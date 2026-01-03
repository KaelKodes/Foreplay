using Godot;
using System;

public partial class GolfHUDController : Control
{
	[Export] public NodePath SwingSystemPath;
	[Export] public NodePath WindSystemPath;

	private SwingSystem _swingSystem;
	private WindSystem _windSystem;

	private ProgressBar _powerBar;
	private ColorRect _accuracyMarker;
	private Control _spinMarker;
	private ColorRect _lockedPowerLine;
	private ColorRect _lockedAccuracyLine;

	private Button _btnNextShot;
	private Label _strokeLabel;
	private Label _carryLabel;
	private Label _totalLabel;
	private Label _speedLabel;
	private SpinBox _powerOverrideSpin;

	private TextureRect _windArrow;
	private Label _windLabel;
	private SpinBox _windSpeedSpin;

	private float _maxSpeed = 0.0f;

	private const float BAR_MIN = 0.0f;
	private const float BAR_MAX = 100.0f;
	private const float BAR_RANGE = BAR_MAX - BAR_MIN;

	public override void _Ready()
	{
		_swingSystem = GetNodeOrNull<SwingSystem>(SwingSystemPath);
		if (_swingSystem == null) _swingSystem = GetTree().CurrentScene.FindChild("SwingSystem", true, false) as SwingSystem;
		_windSystem = GetNodeOrNull<WindSystem>(WindSystemPath);
		if (_windSystem == null) _windSystem = GetTree().CurrentScene.FindChild("WindSystem", true, false) as WindSystem;

		_powerBar = GetNode<ProgressBar>("SwingContainer/PowerBar");
		_accuracyMarker = GetNode<ColorRect>("SwingContainer/AccuracyMarker");
		_lockedPowerLine = GetNode<ColorRect>("SwingContainer/LockedPowerLine");
		_lockedAccuracyLine = GetNode<ColorRect>("SwingContainer/LockedAccuracyLine");
		_spinMarker = GetNode<Control>("SpinSelection/SpinMarker");

		_windArrow = GetNode<TextureRect>("WindContainer/WindArrow");
		_windLabel = GetNode<Label>("WindContainer/WindLabel");
		_windSpeedSpin = GetNode<SpinBox>("WindContainer/WindSpeedSpin");

		_btnNextShot = GetNode<Button>("SwingContainer/BtnNextShot");
		_strokeLabel = GetNode<Label>("StatsPanel/StrokeLabel");
		_carryLabel = GetNode<Label>("StatsPanel/DistanceLabel");
		_totalLabel = GetNode<Label>("StatsPanel/TotalLabel");
		_speedLabel = GetNode<Label>("StatsPanel/SpeedLabel");
		_powerOverrideSpin = GetNode<SpinBox>("StatsPanel/PowerOverrideSpin");

		// Connect Signals
		if (_swingSystem != null)
		{
			_swingSystem.Connect(SwingSystem.SignalName.SwingValuesUpdated, new Callable(this, MethodName.OnSwingValuesUpdated));
			_swingSystem.Connect(SwingSystem.SignalName.ShotDistanceUpdated, new Callable(this, MethodName.OnShotDistanceUpdated));
			_swingSystem.Connect(SwingSystem.SignalName.ShotResult, new Callable(this, MethodName.OnShotResult));
			_swingSystem.Connect(SwingSystem.SignalName.StrokeUpdated, new Callable(this, MethodName.OnStrokeUpdated));
		}

		if (_windSystem != null)
		{
			_windSystem.WindChanged += OnWindChanged;
			OnWindChanged(_windSystem.WindDirection, _windSystem.WindSpeedMph);
		}

		GetNode<Button>("StatsPanel/ResetBtn").Pressed += () => _swingSystem?.ResetMatch();
		GetNode<Button>("StatsPanel/ExitGolfBtn").Pressed += () => _swingSystem?.ExitGolfMode();
		GetNode<Button>("StatsPanel/ToggleWindBtn").Pressed += OnWindTogglePressed;
		_btnNextShot.Pressed += OnNextShotPressed;

		if (_windSpeedSpin != null)
		{
			_windSpeedSpin.ValueChanged += (val) => _windSystem?.SetWindSpeed((float)val);
			_windSpeedSpin.GetLineEdit().FocusMode = FocusModeEnum.None;
		}

		if (_powerOverrideSpin != null)
		{
			_powerOverrideSpin.ValueChanged += (val) => _swingSystem?.SetPowerOverride((float)val);
			_powerOverrideSpin.GetLineEdit().FocusMode = FocusModeEnum.None;
			if (_swingSystem != null)
			{
				_powerOverrideSpin.Value = _swingSystem.GetEstimatedPower();
			}
		}

		// Directional Buttons
		string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
		foreach (var dir in dirs)
		{
			var btn = GetNode<Button>($"WindContainer/WindDirGrid/Btn{dir}");
			btn.Pressed += () => OnWindDirPressed(dir);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;

		if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
		{
			if (GetNode<Control>("SpinSelection").GetGlobalRect().HasPoint(mouseBtn.Position))
			{
				UpdateSpinIntent(mouseBtn.Position);
				return;
			}

			// Check if click is on UI
			if (IsPointOnInteractionUI(mouseBtn.Position)) return;

			_swingSystem?.HandleInput();
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("ui_accept", false))
		{
			_swingSystem?.HandleInput();
			GetViewport().SetInputAsHandled();
		}
	}

	private bool IsPointOnInteractionUI(Vector2 pos)
	{
		// StatsPanel and its children
		if (GetNode<Control>("StatsPanel").GetGlobalRect().HasPoint(pos)) return true;
		if (GetNode<Control>("WindContainer").GetGlobalRect().HasPoint(pos)) return true;
		if (_btnNextShot.Visible && _btnNextShot.GetGlobalRect().HasPoint(pos)) return true;
		return false;
	}

	private void OnNextShotPressed()
	{
		_btnNextShot.Visible = false;
		_swingSystem?.PrepareNextShot();
	}

	private void OnStrokeUpdated(int stroke)
	{
		_strokeLabel.Text = $"Stroke: {stroke}";
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
		GetNode<Button>("StatsPanel/ToggleWindBtn").Text = _windSystem.IsWindEnabled ? "Wind: ON" : "Wind: OFF";
		GetNode<Control>("WindContainer").Visible = _windSystem.IsWindEnabled;
	}

	private void OnWindChanged(Vector3 direction, float speedMph)
	{
		_windLabel.Text = $"{speedMph:F0} mph";
		if (_windSpeedSpin != null) _windSpeedSpin.Value = speedMph;
		_windArrow.Rotation = Mathf.Atan2(-direction.X, direction.Z);
	}

	private void OnShotResult(float power, float accuracy)
	{
		GD.Print($"GolfHUD: Result Power={power}, Acc={accuracy}");
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
			if (carry == 0) { _speedLabel.Text = "Speed: 0.0 m/s"; _maxSpeed = 0.0f; }
		}

		if (total > 0.01f && carry != -2.0f)
		{
			_totalLabel.Text = $"Total: {total:F1}y";
			_speedLabel.Text = $"Max Speed: {_maxSpeed:F1} m/s";
			_btnNextShot.Visible = true;
		}
		else if (total == 0.0f) _totalLabel.Text = "Total: 0.0y";
	}

	private void OnSwingValuesUpdated(float currentBarValue, float lockedPower, float lockedAccuracy)
	{
		_powerBar.Value = currentBarValue;
		float parentWidth = _powerBar.Size.X;

		if (lockedAccuracy < 0)
		{
			float ratio = currentBarValue / 100.0f;
			_accuracyMarker.Position = new Vector2(ratio * parentWidth - (_accuracyMarker.Size.X / 2.0f), _accuracyMarker.Position.Y);
			_accuracyMarker.Visible = true;
		}
		else _accuracyMarker.Visible = false;

		_lockedPowerLine.Visible = (lockedPower >= 0);
		if (lockedPower >= 0)
		{
			float ratio = lockedPower / 100.0f;
			_lockedPowerLine.Position = new Vector2(ratio * parentWidth - (_lockedPowerLine.Size.X / 2.0f), _lockedPowerLine.Position.Y);

			// Perfect Power color (94% target)
			bool isPerfectPower = Mathf.Abs(lockedPower - 94.0f) <= 1.5f; // Small buffer for frame timing
			_lockedPowerLine.Modulate = isPerfectPower ? Colors.Green : Colors.White;
		}

		_lockedAccuracyLine.Visible = (lockedAccuracy >= 0);
		if (lockedAccuracy >= 0)
		{
			float ratio = lockedAccuracy / 100.0f;
			_lockedAccuracyLine.Position = new Vector2(ratio * parentWidth - (_lockedAccuracyLine.Size.X / 2.0f), _lockedAccuracyLine.Position.Y);

			// Perfect Accuracy color (25% target)
			bool isPerfectAcc = Mathf.Abs(lockedAccuracy - 25.0f) <= 1.0f;
			_lockedAccuracyLine.Modulate = isPerfectAcc ? Colors.Green : Colors.White;
		}
	}

	private void UpdateSpinIntent(Vector2 globalPos)
	{
		Control spinContainer = GetNode<Control>("SpinSelection");
		Vector2 localPos = spinContainer.GetGlobalTransform().AffineInverse() * globalPos;
		Vector2 normalizedSpin = new Vector2((localPos.X / spinContainer.Size.X) * 2.0f - 1.0f, (localPos.Y / spinContainer.Size.Y) * 2.0f - 1.0f);
		_spinMarker.Position = localPos - (_spinMarker.Size / 2.0f);
		_swingSystem?.SetSpinIntent(normalizedSpin);
	}
}
