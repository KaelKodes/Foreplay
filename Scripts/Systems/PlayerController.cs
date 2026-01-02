using Godot;
using System;

public enum PlayerState
{
    Idle,       // Walking / Roaming
    Golfing,    // Aiming / Swinging
    Driving,    // In a vehicle
    Surveying   // Placing points for terrain
}

public partial class PlayerController : CharacterBody3D
{
    [Export] public float RotationSpeed = 1.0f;
    [Export] public float MoveSpeed = 5.0f;
    [Export] public float JumpForce = 5.0f;
    [Export] public float Gravity = 9.8f;
    [Export] public NodePath CameraPath;
    [Export] public NodePath SwingSystemPath;

    // Physics State
    private Vector3 _velocity = Vector3.Zero;
    private bool _isGrounded = true;

    // Multiplayer Properties
    [Export] public int PlayerIndex { get; set; } = 0; // 0=Blue, 1=Red, 2=Green, 3=Yellow
    public bool IsLocal { get; set; } = true;
    private PlayerState _currentState = PlayerState.Idle;
    public PlayerState CurrentState
    {
        get => _currentState;
        set
        {
            _currentState = value;
            if (_facingArrow != null) _facingArrow.Visible = (_currentState == PlayerState.Idle);
        }
    }

    private CameraController _camera;
    private MeshInstance3D _avatarMesh;
    private SwingSystem _swingSystem;
    private MeshInstance3D _facingArrow;
    private GolfCart _currentCart;

    public override void _Ready()
    {
        if (CameraPath != null) _camera = GetNode<CameraController>(CameraPath);
        if (SwingSystemPath != null) _swingSystem = GetNode<SwingSystem>(SwingSystemPath);

        // Attempt to find the visual avatar
        _avatarMesh = GetNodeOrNull<MeshInstance3D>("AvatarMesh");
        UpdatePlayerColor();

        // Create Facing Arrow (Visual Feedback)
        _facingArrow = new MeshInstance3D();
        var prism = new PrismMesh();
        prism.Size = new Vector3(0.5f, 0.5f, 0.1f); // Flat arrow
        _facingArrow.Mesh = prism;
        _facingArrow.Position = new Vector3(0, 0.1f, 0.8f); // At feet (just above ground)
        _facingArrow.RotationDegrees = new Vector3(-90, 180, 0); // Pointing Forward

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = Colors.Orange;
        _facingArrow.MaterialOverride = mat;

        AddChild(_facingArrow);

        // Force initial orientation facing down-range (+Z)
        RotationDegrees = new Vector3(0, 180, 0);
    }

    public void SetPlayerIndex(int index)
    {
        PlayerIndex = index;
        UpdatePlayerColor();
    }

    private void UpdatePlayerColor()
    {
        if (_avatarMesh == null) return;

        Color c = Colors.Blue;
        switch (PlayerIndex % 4)
        {
            case 0: c = Colors.Blue; break;
            case 1: c = Colors.Red; break;
            case 2: c = Colors.Green; break;
            case 3: c = Colors.Yellow; break;
        }

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = c;
        if (_avatarMesh != null) _avatarMesh.MaterialOverride = mat;
    }

    public override void _PhysicsProcess(double delta)
    {
        // 1. Authority Check: Only process input if this is OUR player
        if (!IsLocal) return;

        // 2. State Check
        if (CurrentState == PlayerState.Golfing)
        {
            HandleGolfingInput(delta);
        }
        else if (CurrentState == PlayerState.Idle)
        {
            HandleWalkingInput(delta);
        }
        else if (CurrentState == PlayerState.Driving)
        {
            HandleDrivingInput(delta);
        }
        else if (CurrentState == PlayerState.Surveying)
        {
            HandleSurveyingInput(delta);
        }
    }

    private void HandleGolfingInput(double delta)
    {
        if (_camera == null || _swingSystem == null) return;

        // "Mouse Aim": Only sync if ball is stationary (Idle or choosing power/acc)
        if (_swingSystem.CurrentStage == SwingStage.Executing || _swingSystem.CurrentStage == SwingStage.ShotComplete) return;

        // Continuously sync player position/rotation to camera's horizontal heading.
        // This ensures the golfer stays in the correct stance relative to the aim line.
        Vector3 ballPos = _swingSystem.BallPosition;
        Vector3 camForward = -_camera.GlobalTransform.Basis.Z;
        camForward.Y = 0;

        if (camForward.LengthSquared() < 0.01f) return;
        camForward = camForward.Normalized();

        // Position player 0.75m to the "left" of the aim line
        Vector3 leftDir = new Vector3(camForward.Z, 0, -camForward.X);
        Vector3 stancePos = ballPos + (leftDir * 0.75f);
        stancePos.Y = ballPos.Y + 0.1f;

        GlobalPosition = stancePos;

        // Face the ball
        LookAt(ballPos, Vector3.Up);
        RotationDegrees = new Vector3(0, RotationDegrees.Y, 0);
    }

    private void HandleWalkingInput(double delta)
    {
        if (_camera == null) return;

        // Gravity
        if (!IsOnFloor())
        {
            _velocity.Y -= Gravity * (float)delta;
        }
        else
        {
            _velocity.Y = 0;
        }

        // Jump
        if (IsOnFloor() && Input.IsActionJustPressed("ui_accept"))
        {
            _velocity.Y = JumpForce;
        }

        // Movement
        Vector3 inputDir = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) inputDir.Z -= 1;
        if (Input.IsKeyPressed(Key.S)) inputDir.Z += 1;
        if (Input.IsKeyPressed(Key.A)) inputDir.X -= 1;
        if (Input.IsKeyPressed(Key.D)) inputDir.X += 1;

        if (inputDir.LengthSquared() > 0.1f)
        {
            inputDir = inputDir.Normalized();

            // Rotate input to match Camera Y rotation
            Vector3 camRot = _camera.GlobalRotation;
            Vector3 moveDir = inputDir.Rotated(Vector3.Up, camRot.Y);

            // Sprint Logic
            float speedMult = Input.IsKeyPressed(Key.Shift) ? 2.0f : 1.0f;

            _velocity.X = moveDir.X * MoveSpeed * speedMult;
            _velocity.Z = moveDir.Z * MoveSpeed * speedMult;

            // Rotate player to face movement
            float targetAngle = Mathf.Atan2(moveDir.X, moveDir.Z);
            float currentAngle = Rotation.Y;
            Rotation = new Vector3(0, Mathf.LerpAngle(currentAngle, targetAngle, 10.0f * (float)delta), 0);
        }
        else
        {
            // Decelerate X/Z
            _velocity.X = Mathf.MoveToward(_velocity.X, 0, MoveSpeed * 5.0f * (float)delta);
            _velocity.Z = Mathf.MoveToward(_velocity.Z, 0, MoveSpeed * 5.0f * (float)delta);
        }

        // Apply Velocity using CharacterBody3D's built-in physics
        Velocity = _velocity;
        MoveAndSlide();
        _velocity = Velocity;

        // Update grounded state from CharacterBody3D
        _isGrounded = IsOnFloor();

        // Interaction Logic
        if (_swingSystem != null)
        {
            float distToTee = GlobalPosition.DistanceTo(_swingSystem.TeePosition);
            float distToBall = GlobalPosition.DistanceTo(_swingSystem.BallPosition);
            bool isBallInField = _swingSystem.BallPosition.DistanceTo(_swingSystem.TeePosition) > 1.0f;

            // Check Ghost Markers
            bool nearGhostMarker = false;
            foreach (var ghost in _swingSystem.GhostMarkers)
            {
                if (IsInstanceValid(ghost) && GlobalPosition.DistanceTo(ghost.GlobalPosition) < 2.0f)
                {
                    nearGhostMarker = true;
                    break;
                }
            }

            if (distToTee < 2.0f || nearGhostMarker)
            {
                if (isBallInField)
                {
                    _swingSystem.SetPrompt(true, "E: GOTO BALL | T: RESET");
                    if (Input.IsKeyPressed(Key.E))
                    {
                        // Teleport to Ball vicinity
                        Vector3 targetPos = _swingSystem.BallPosition + new Vector3(0, 0, 1.0f);
                        TeleportTo(targetPos, _swingSystem.BallPosition);
                        // Automatically enter Golf Mode
                        _swingSystem.EnterGolfMode();
                    }
                    else if (Input.IsKeyPressed(Key.T))
                    {
                        _swingSystem.ResetMatch();
                    }
                }
                else
                {
                    _swingSystem.SetPrompt(true, "PRESS E TO TEE OFF");
                    if (Input.IsKeyPressed(Key.E))
                    {
                        _swingSystem.EnterGolfMode();
                    }
                }
            }
            else if (distToBall < 2.0f && isBallInField)
            {
                _swingSystem.SetPrompt(true, "PRESS E TO GOLF");
                if (Input.IsKeyPressed(Key.E))
                {
                    _swingSystem.EnterGolfMode();
                }
            }
            else
            {
                _swingSystem.SetPrompt(false);
            }
        }

        // Search for nearby Golf Carts
        var carts = GetTree().GetNodesInGroup("carts");
        GolfCart nearestCart = null;
        float minDist = 3.0f;

        foreach (Node node in carts)
        {
            if (node is GolfCart cart)
            {
                float d = GlobalPosition.DistanceTo(cart.GlobalPosition);
                if (d < minDist)
                {
                    minDist = d;
                    nearestCart = cart;
                }
            }
        }

        if (nearestCart != null && !nearestCart.IsBeingDriven)
        {
            if (_swingSystem != null) _swingSystem.SetPrompt(true, "PRESS E TO DRIVE");
            if (Input.IsActionJustPressed("ui_accept") || Input.IsKeyPressed(Key.E))
            {
                EnterVehicle(nearestCart);
            }
        }
    }

    private void HandleDrivingInput(double delta)
    {
        if (_currentCart == null)
        {
            CurrentState = PlayerState.Idle;
            return;
        }

        // Stick player to cart position (relative offset)
        GlobalPosition = _currentCart.GlobalPosition + _currentCart.Transform.Basis.Y * 0.5f;
        Rotation = _currentCart.Rotation;

        if (Input.IsActionJustPressed("ui_cancel") || Input.IsKeyPressed(Key.E))
        {
            ExitVehicle();
        }
    }

    private void HandleSurveyingInput(double delta)
    {
        // Surveying uses standard walking controls for movement
        HandleWalkingInput(delta);

        // Interaction logic for Surveying is handled by the SurveyManager
    }

    private void EnterVehicle(GolfCart cart)
    {
        _currentCart = cart;
        _currentCart.Enter(this);
        CurrentState = PlayerState.Driving;
        Visible = false; // Hide player while driving (or could sit them)

        if (_camera != null)
        {
            _camera.SetTarget(_currentCart, true);
        }
        if (_swingSystem != null) _swingSystem.SetPrompt(false);
    }

    private void ExitVehicle()
    {
        if (_currentCart == null) return;

        _currentCart.Exit();
        CurrentState = PlayerState.Idle;
        Visible = true;

        // Place player next to cart
        GlobalPosition = _currentCart.GlobalPosition + _currentCart.Transform.Basis.X * 2.0f;

        if (_camera != null)
        {
            _camera.SetTarget(this, true);
        }
        _currentCart = null;
    }

    public void TeleportTo(Vector3 position, Vector3 lookAtTarget)
    {
        GlobalPosition = position;

        // Face the target
        // LookAt points -Z towards target, which matches our Forward convention
        LookAt(new Vector3(lookAtTarget.X, GlobalPosition.Y, lookAtTarget.Z), Vector3.Up);

        // Ensure rotation is clean (Y-axis only)
        RotationDegrees = new Vector3(0, RotationDegrees.Y, 0);
    }
}
