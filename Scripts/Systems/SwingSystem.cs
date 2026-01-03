using Godot;
using System;
using System.Collections.Generic;

public enum SwingStage
{
	Idle,
	Power,
	Accuracy,
	Executing,
	ShotComplete
}

public partial class SwingSystem : Node
{
	[Export] public float SwingSpeed = 1.0f;
	[Export] public NodePath BallPath;
	[Export] public NodePath CameraPath;
	[Export] public NodePath WindSystemPath;

	private BallController _ball;
	private CameraController _camera;
	private WindSystem _windSystem;
	private BallLieSystem _lieSystem;
	private StatsService _statsService;
	private MarkerManager _markerManager;
	private BuildManager _buildManager;
	private ObjectPlacer _objectPlacer;

	public BuildManager BuildManager => _buildManager;
	public ObjectPlacer ObjectPlacer => _objectPlacer;

	private SwingStage _stage = SwingStage.Idle;
	private float _timer = 0.0f;
	private bool _isReturnPhase = false;
	private float _lockedPower = -1.0f;
	private float _lockedAccuracy = -1.0f;
	private Vector2 _spinIntent = Vector2.Zero;
	private float _powerOverride = -1.0f;
	private double _lastInputTime = 0;
	private Vector3 _targetPosition = new Vector3(0, 0, 548.64f);
	private PlayerController _currentPlayer;

	[Signal] public delegate void SwingStageChangedEventHandler(int newStage);
	[Signal] public delegate void SwingValuesUpdatedEventHandler(float currentBarValue, float lockedPower, float lockedAccuracy);
	[Signal] public delegate void ShotDistanceUpdatedEventHandler(float carry, float total);
	[Signal] public delegate void ShotResultEventHandler(float power, float accuracy);
	[Signal] public delegate void ModeChangedEventHandler(bool isGolfing);
	[Signal] public delegate void PromptChangedEventHandler(bool visible, string message);
	[Signal] public delegate void StrokeUpdatedEventHandler(int stroke);

	public Vector3 BallPosition => _ball?.GlobalPosition ?? Vector3.Zero;
	public SwingStage CurrentStage => _stage;
	public List<MeshInstance3D> GhostMarkers => _markerManager.GhostMarkers;
	public Vector3 TeePosition { get; private set; } = new Vector3(49.51819f, 0.0f, -59.909077f); // Default to TerrainTest tee

	public override void _Ready()
	{
		var teeBox = GetNodeOrNull<Node3D>("../TeeBox/VisualTee");
		if (teeBox == null) teeBox = GetNodeOrNull<Node3D>("../TeeBox");
		if (teeBox != null)
		{
			TeePosition = teeBox.GlobalPosition;
			teeBox.AddToGroup("targets");
		}

		var placeholder = GetNodeOrNull<PlayerController>("../PlayerPlaceholder");
		if (placeholder != null) RegisterPlayer(placeholder);

		if (BallPath != null)
		{
			_ball = GetNode<BallController>(BallPath);
			_ball.Connect(BallController.SignalName.BallSettled, new Callable(this, MethodName.OnBallSettled));
			_ball.Connect(BallController.SignalName.BallCarried, new Callable(this, MethodName.OnBallCarried));
		}

		if (CameraPath != null) _camera = GetNode<CameraController>(CameraPath);
		if (WindSystemPath != null) _windSystem = GetNode<WindSystem>(WindSystemPath);

		_lieSystem = new BallLieSystem();
		AddChild(_lieSystem);

		_statsService = new StatsService();
		AddChild(_statsService);
		_statsService.LoadStats();

		_markerManager = new MarkerManager();
		AddChild(_markerManager);

		_buildManager = new BuildManager();
		_buildManager.Name = "BuildManager";
		_buildManager.Player = _currentPlayer;
		AddChild(_buildManager);

		_objectPlacer = new ObjectPlacer();
		_objectPlacer.Name = "ObjectPlacer";
		AddChild(_objectPlacer);

		var green = GetNodeOrNull<Node3D>("../Green500");
		if (green == null) green = GetTree().CurrentScene.FindChild("Green*", true, false) as Node3D;
		if (green != null)
		{
			_targetPosition = green.GlobalPosition;
			green.AddToGroup("targets");
			// Also search for Flag or Pin children
			var pin = green.FindChild("Pin", true, false) as Node3D;
			if (pin != null) pin.AddToGroup("targets");
		}

		CallDeferred(MethodName.ExitGolfMode);
	}

	public void UpdateTeePosition(Vector3 pos)
	{
		TeePosition = pos;
		GD.Print($"SwingSystem: TeePosition updated to {pos}");
	}

	public void UpdatePinPosition(Vector3 pos)
	{
		_targetPosition = pos;
		GD.Print($"SwingSystem: PinPosition updated to {pos}");
	}

	public void RegisterPlayer(PlayerController player)
	{
		_currentPlayer = player;
		if (_buildManager != null) _buildManager.Player = player;
		GD.Print($"SwingSystem: Registered Player {player.Name}");
	}

	public void SetPrompt(bool visible, string message = "") => EmitSignal(SignalName.PromptChanged, visible, message);

	public void EnterGolfMode()
	{
		if (_currentPlayer == null) return;
		_markerManager.UpdateBallIndicator(false, Vector3.Zero, 0);
		_currentPlayer.CurrentState = PlayerState.GolfMode;
		SetPlayerStance();

		if (_camera != null)
		{
			Vector3 dirToTarget = (_targetPosition - _ball.GlobalPosition).Normalized();
			float targetYaw = Mathf.Atan2(dirToTarget.X, dirToTarget.Z) + Mathf.Pi;
			_camera.Rotation = new Vector3(Mathf.DegToRad(-20), targetYaw, 0);
			_camera.SetTarget(_ball, true);
			_camera.SetFollowing(false);
		}

		_markerManager.SetGhostMarkersVisible(false);
		EmitSignal(SignalName.ModeChanged, true);
		SetPrompt(false);
	}

	public void ExitGolfMode()
	{
		if (_currentPlayer == null) return;
		_currentPlayer.CurrentState = PlayerState.WalkMode;

		if (_camera != null)
		{
			_camera.SetTarget(_currentPlayer, true);
			_camera.SetFollowing(false);
			_camera.SetFreeLook(false);
		}
		_markerManager.SetGhostMarkersVisible(true);
		EmitSignal(SignalName.ModeChanged, false);
	}


	public void EnterBuildMode()
	{
		if (_currentPlayer == null) return;
		ExitGolfMode();
		_currentPlayer.CurrentState = PlayerState.BuildMode;
		SetPrompt(false);
	}


	public void ExitBuildMode()
	{
		if (_currentPlayer == null) return;
		_currentPlayer.CurrentState = PlayerState.WalkMode;
		SetPrompt(false);
	}

	private void OnBallCarried(float carry) => EmitSignal(SignalName.ShotDistanceUpdated, carry, -1);

	private void OnBallSettled(float total)
	{
		EmitSignal(SignalName.ShotDistanceUpdated, -1, total);
		_stage = SwingStage.ShotComplete;
		if (_camera != null) _camera.SetFreeLook(true);
		_markerManager.UpdateBallIndicator(true, _ball.GlobalPosition, _currentPlayer?.PlayerIndex ?? 0);
	}

	public override void _Process(double delta)
	{
		if ((_stage == SwingStage.Executing || _stage == SwingStage.Idle || _stage == SwingStage.ShotComplete) && _ball != null && _ball.LinearVelocity.Length() > 0.1f)
		{
			EmitSignal(SignalName.ShotDistanceUpdated, -2, _ball.LinearVelocity.Length());
		}

		if (_stage == SwingStage.Power)
		{
			float speed = SwingSpeed * 100.0f;
			if (!_isReturnPhase)
			{
				_timer += (float)delta * speed;
				if (_timer >= 100.0f) { _timer = 100.0f; _isReturnPhase = true; }
			}
			else
			{
				_timer -= (float)delta * speed;
				if (_timer < 50.0f && _lockedPower < 0) { CancelSwing(); return; }
				if (_timer <= 0.0f)
				{
					if (_lockedPower > 0) { _lockedAccuracy = 0.0f; ExecuteShot(); }
					else CancelSwing();
					return;
				}
			}
			EmitSignal(SignalName.SwingValuesUpdated, _timer, _lockedPower, _lockedAccuracy);
		}
	}

	public void HandleInput()
	{
		if (_currentPlayer == null || !_currentPlayer.IsLocal) return;
		double currentTime = Time.GetTicksMsec();
		if (currentTime - _lastInputTime < 250.0) return;

		if (_stage == SwingStage.Idle) StartSwing();
		else if (_stage == SwingStage.Power) ProcessCycleInput();
	}

	private void StartSwing()
	{
		_lastInputTime = Time.GetTicksMsec();
		_stage = SwingStage.Power;
		_timer = 0.0f;
		_isReturnPhase = false;
		_lockedPower = -1.0f;
		_lockedAccuracy = -1.0f;
		EmitSignal(SignalName.SwingStageChanged, (int)_stage);
	}

	private void ProcessCycleInput()
	{
		_lastInputTime = Time.GetTicksMsec();
		if (_lockedPower < 0) { _lockedPower = _timer; return; }
		if (_isReturnPhase && _timer <= 50.0f)
		{
			_lockedAccuracy = _timer;
			_stage = SwingStage.Executing;
			EmitSignal(SignalName.SwingStageChanged, (int)_stage);
			EmitSignal(SignalName.SwingValuesUpdated, _timer, _lockedPower, _lockedAccuracy);
			ExecuteShot();
		}
	}

	private void ExecuteShot()
	{
		EmitSignal(SignalName.ShotResult, _lockedPower, _lockedAccuracy);

		var shotParams = new ShotPhysics.ShotParams
		{
			PowerValue = _lockedPower,
			AccuracyValue = (_lockedAccuracy >= 0) ? _lockedAccuracy : 0.0f,
			SpinIntent = _spinIntent,
			PowerOverride = _powerOverride,
			PlayerStats = _statsService.PlayerStats,
			CurrentLie = _lieSystem.GetCurrentLie(_ball.GlobalPosition),
			CameraCameraForward = -GetViewport().GetCamera3D().GlobalTransform.Basis.Z,
			IsRightHanded = _statsService.PlayerStats.IsRightHanded
		};

		var result = ShotPhysics.CalculateShot(shotParams);

		if (_windSystem != null) _ball.SetWind(_windSystem.GetWindVelocityVector());

		_ball.Launch(result.Velocity, result.Spin);
		if (_camera != null) _camera.SetFollowing(true);

		_statsService.UpdateAnger(_lockedAccuracy - 25.0f);
	}

	private int _strokeCount = 1;

	public void PrepareNextShot()
	{
		_markerManager.CreateGhostMarker(_ball.GlobalPosition + new Vector3(0, 1.5f, 0), _currentPlayer?.PlayerIndex ?? 0);
		_strokeCount++;
		EmitSignal(SignalName.StrokeUpdated, _strokeCount);
		_ball.PrepareNextShot();
		ExitGolfMode();
		_stage = SwingStage.Idle;
		_timer = 0.0f;
		_isReturnPhase = false;
		_lockedPower = -1.0f;
		_lockedAccuracy = -1.0f;

		if (_camera != null) { _camera.SetFollowing(false); _camera.SetFreeLook(false); }
		EmitSignal(SignalName.SwingStageChanged, (int)_stage);
		EmitSignal(SignalName.SwingValuesUpdated, 0, -1, -1);
		EmitSignal(SignalName.ShotDistanceUpdated, 0, 0);
	}

	public void CancelSwing()
	{
		_stage = SwingStage.Idle;
		_timer = 0.0f;
		_isReturnPhase = false;
		_lockedPower = -1.0f;
		_lockedAccuracy = -1.0f;
		EmitSignal(SignalName.SwingStageChanged, (int)_stage);
		EmitSignal(SignalName.SwingValuesUpdated, 0, -1, -1);
	}

	public void ResetMatch()
	{
		_markerManager.ClearGhostMarkers();
		_strokeCount = 1;
		EmitSignal(SignalName.StrokeUpdated, _strokeCount);
		_stage = SwingStage.Idle;
		_timer = 0.0f;
		_isReturnPhase = false;
		_lockedPower = -1.0f;
		_lockedAccuracy = -1.0f;
		_ball.Reset();
		_markerManager.UpdateBallIndicator(false, Vector3.Zero, 0);
		SetPlayerStance();

		if (_camera != null) { _camera.SetFollowing(false); _camera.SetFreeLook(false); }
		EmitSignal(SignalName.SwingStageChanged, (int)_stage);
		EmitSignal(SignalName.SwingValuesUpdated, 0, -1, -1);
		EmitSignal(SignalName.ShotDistanceUpdated, 0, 0);
	}

	public void SetSpinIntent(Vector2 intent) { if (_stage == SwingStage.Idle) _spinIntent = intent; }
	public float GetEstimatedPower()
	{
		float val = (_powerOverride > 0) ? _powerOverride : _statsService.PlayerStats.Power;
		return val;
	}

	public void SetPowerOverride(float power)
	{
		_powerOverride = power;
		GD.Print($"SwingSystem: PowerOverride UPDATED to {power}");
	}

	public float GetAnger() => _statsService.PlayerStats.Anger;

	private void SetPlayerStance()
	{
		if (_currentPlayer == null || _ball == null) return;
		Vector3 ballPos = _ball.GlobalPosition;
		Vector3 targetDir = (_targetPosition - ballPos).Normalized();
		targetDir.Y = 0;
		Vector3 leftDir = new Vector3(targetDir.Z, 0, -targetDir.X);
		Vector3 stancePos = ballPos + (leftDir * 0.75f);
		stancePos.Y = ballPos.Y + 0.1f;
		_currentPlayer.GlobalPosition = stancePos;
		_currentPlayer.LookAt(ballPos, Vector3.Up);
		_currentPlayer.RotationDegrees = new Vector3(0, _currentPlayer.RotationDegrees.Y, 0);
	}
}
