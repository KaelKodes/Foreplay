using Godot;
using System;

public enum PlayerState
{
    WalkMode,       // Formerly Idle
    GolfMode,       // Aiming / Swinging
    BuildMode,      // Formerly Surveying
    DriveMode,      // In a vehicle
    SpectateMode,   // Free-cam / Spectating
    PlacingObject   // Manipulating an object (Move/Place)
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
    private PlayerState _currentState = PlayerState.WalkMode;
    public PlayerState CurrentState
    {
        get => _currentState;
        set
        {
            GD.Print($"[PlayerController] State changing from {_currentState} to {value}");
            _currentState = value;
            if (_facingArrow != null) _facingArrow.Visible = (_currentState == PlayerState.WalkMode);
        }
    }

    private CameraController _camera;
    private MeshInstance3D _avatarMesh;
    private SwingSystem _swingSystem;
    private MeshInstance3D _facingArrow;
    private GolfCart _currentCart;
    private InteractableObject _selectedObject;
    public InteractableObject SelectedObject => _selectedObject;
    private InteractableObject _lastHoveredObject;
    private MainHUDController _hud;

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

        _hud = GetTree().CurrentScene.FindChild("HUD", true, false) as MainHUDController;

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
        switch (CurrentState)
        {
            case PlayerState.GolfMode:
                HandleGolfingInput(delta);
                break;
            case PlayerState.WalkMode:
                HandleWalkingInput(delta);
                break;
            case PlayerState.DriveMode:
                HandleDrivingInput(delta);
                break;
            case PlayerState.BuildMode:
                HandleBuildModeInput(delta);
                break;
            case PlayerState.SpectateMode:
                // TODO: Free-look cam
                break;
        }
    }

    private void HandleGolfingInput(double delta)
    {
        if (_camera == null || _swingSystem == null) return;

        // "Mouse Aim": Only sync if ball is stationary (Idle or choosing power/acc)
        if (_swingSystem.CurrentStage == SwingStage.Executing || _swingSystem.CurrentStage == SwingStage.ShotComplete) return;

        // Continuously sync player position/rotation to camera's horizontal heading.
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
        HandleProximityPrompts();

        // Search for nearby Golf Carts
        HandleVehicleDetection();
    }

    private void HandleProximityPrompts()
    {
        if (_swingSystem == null) return;

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
                    Vector3 targetPos = _swingSystem.BallPosition + new Vector3(0, 0, 1.0f);
                    TeleportTo(targetPos, _swingSystem.BallPosition);
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
            // Generic Interaction Check
            InteractableObject hitObj = CheckInteractionForwardRaycast();
            if (hitObj != null)
            {
                float dist = GlobalPosition.DistanceTo(hitObj.GlobalPosition);
                // GD.Print($"IO Found: {hitObj.Name}, Dist: {dist}");

                if (dist < 5.0f)
                {
                    string prompt = hitObj.GetInteractionPrompt();
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        _swingSystem.SetPrompt(true, prompt);
                        if (Input.IsKeyPressed(Key.E))
                        {
                            hitObj.OnInteract(this);
                        }
                        return; // Priority over clearing
                    }
                }
            }

            _swingSystem.SetPrompt(false);
        }
    }

    private InteractableObject CheckInteractionForwardRaycast()
    {
        // Cast from Player Body forward, not Camera
        var spaceState = GetWorld3D().DirectSpaceState;

        // Origin: Approx Head Height
        var from = GlobalPosition + new Vector3(0, 1.5f, 0);
        // Direction: Player Forward (+Z because Basis.Z seems to be Forward for this mesh?)
        var to = from + GlobalTransform.Basis.Z * 3.0f; // 3m reach

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 3; // Layers 1 and 2
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var hitObj = (Node)result["collider"];
            // GD.Print($"Hit: {((Node)hitObj).Name}");

            if (hitObj is Node colliderNode)
            {
                Node n = colliderNode;
                while (n != null)
                {
                    if (n is InteractableObject io) return io;
                    n = n.GetParent();
                }
            }
        }
        return null;
    }

    private void HandleVehicleDetection()
    {
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
            if (Input.IsKeyPressed(Key.E))
            {
                EnterVehicle(nearestCart);
            }
        }
    }

    private void HandleDrivingInput(double delta)
    {
        if (_currentCart == null)
        {
            CurrentState = PlayerState.WalkMode;
            return;
        }

        GlobalPosition = _currentCart.GlobalPosition + _currentCart.Transform.Basis.Y * 0.5f;
        Rotation = _currentCart.Rotation;

        if (Input.IsKeyPressed(Key.E))
        {
            ExitVehicle();
        }
    }

    private void HandleBuildModeInput(double delta)
    {
        // Standard movement allowed in build mode
        HandleWalkingInput(delta);

        // Tool-specific behavior
        if (_hud != null && _hud.CurrentTool != MainHUDController.BuildTool.Selection)
        {
            // If not in selection tool, don't allow selecting/editing objects
            if (_selectedObject != null) _selectedObject.SetSelected(false);
            _selectedObject = null;
            return;
        }

        // Selection feedback (Highlight on hover)
        InteractableObject hoverObj = CheckInteractableRaycast();

        if (hoverObj != _lastHoveredObject)
        {
            if (_lastHoveredObject != null && _lastHoveredObject != _selectedObject)
            {
                _lastHoveredObject.OnHover(false);
            }
            _lastHoveredObject = hoverObj;
        }

        if (hoverObj != null && hoverObj != _selectedObject)
        {
            hoverObj.OnHover(true);
        }

        // Handle Selected Object Actions
        if (_selectedObject != null)
        {
            if (Input.IsKeyPressed(Key.X) && _selectedObject.IsDeletable)
            {
                _selectedObject.QueueFree();
                _selectedObject = null;
                _swingSystem.SetPrompt(false);
            }
            else if (Input.IsKeyPressed(Key.C) && _selectedObject.IsMovable)
            {
                if (_swingSystem.ObjectPlacer != null)
                {
                    var objToMove = _selectedObject;
                    _selectedObject.SetSelected(false);
                    _selectedObject = null;
                    _swingSystem.ObjectPlacer.StartPlacing(objToMove);
                }
            }

            if (_selectedObject != null)
            {
                _swingSystem.SetPrompt(true, $"SELECTED: {_selectedObject.ObjectName} | X: DELETE | C: REPOSITION");
            }
        }
        else if (hoverObj != null)
        {
            _swingSystem.SetPrompt(true, $"CLICK TO SELECT {hoverObj.ObjectName}");
        }
        else
        {
            // Only clear if we aren't displaying something else from BuildManager
            // (Note: BuildManager might be setting prompts too, so we need to be careful)
        }
    }

    private void EnterVehicle(GolfCart cart)
    {
        _currentCart = cart;
        _currentCart.Enter(this);
        CurrentState = PlayerState.DriveMode;
        Visible = false;

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
        CurrentState = PlayerState.WalkMode;
        Visible = true;

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
        LookAt(new Vector3(lookAtTarget.X, GlobalPosition.Y, lookAtTarget.Z), Vector3.Up);
        RotationDegrees = new Vector3(0, RotationDegrees.Y, 0);
    }

    private InteractableObject CheckInteractableRaycast()
    {
        if (_camera == null) return null;

        var mousePos = GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mousePos);
        var to = from + _camera.ProjectRayNormal(mousePos) * 100.0f;

        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(from, to);

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var collider = (Node)result["collider"];
            var interactable = collider.GetNodeOrNull<InteractableObject>(".") ?? collider.GetParentOrNull<InteractableObject>();
            if (interactable == null && collider.GetParent() != null)
            {
                interactable = collider.GetParent().GetParentOrNull<InteractableObject>();
            }
            return interactable;
        }
        return null;
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsLocal) return;

        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.V)
        {
            if (CurrentState == PlayerState.WalkMode)
            {
                _swingSystem.EnterBuildMode();
            }
            else if (CurrentState == PlayerState.BuildMode)
            {
                if (_selectedObject != null) _selectedObject.SetSelected(false);
                _selectedObject = null;
                _swingSystem.ExitBuildMode();
            }
        }

        if (@event is InputEventKey homeKey && homeKey.Pressed && !homeKey.Echo && homeKey.Keycode == Key.Home)
        {
            if (_swingSystem != null)
            {
                Vector3 teePos = _swingSystem.TeePosition;
                // Teleport slightly behind and above the tee
                Vector3 offset = new Vector3(0, 0, -1.0f); // Face down-range (+Z)
                TeleportTo(teePos + offset, teePos + Vector3.Forward * 10.0f);
                GD.Print("PlayerController: Home teleport to Tee.");
            }
        }
        // Selection logic in Build Mode
        if (CurrentState == PlayerState.BuildMode)
        {
            // Mouse Wheel actions
            if (_selectedObject != null && @event is InputEventMouseButton mbScroll && mbScroll.Pressed)
            {
                bool isShift = mbScroll.ShiftPressed;
                bool isCtrl = mbScroll.CtrlPressed;

                if (mbScroll.ButtonIndex == MouseButton.WheelUp)
                {
                    if (isCtrl)
                        _selectedObject.Scale *= 1.1f;
                    else if (isShift)
                        _selectedObject.GlobalPosition += new Vector3(0, 0.25f, 0);
                    else
                        _selectedObject.RotateY(Mathf.DegToRad(15.0f));

                    GetViewport().SetInputAsHandled();
                }
                else if (mbScroll.ButtonIndex == MouseButton.WheelDown)
                {
                    if (isCtrl)
                        _selectedObject.Scale *= 0.9f;
                    else if (isShift)
                        _selectedObject.GlobalPosition -= new Vector3(0, 0.25f, 0);
                    else
                        _selectedObject.RotateY(Mathf.DegToRad(-15.0f));

                    GetViewport().SetInputAsHandled();
                }
            }

            // Mouse Drag Rotation
            if (_selectedObject != null && @event is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Left))
            {
                // Rotate around Y axis based on horizontal mouse movement
                float rotSpeed = 0.5f;
                _selectedObject.RotateY(Mathf.DegToRad(mm.Relative.X * rotSpeed));
            }

            // Select on Click
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                if (_hud == null || _hud.CurrentTool == MainHUDController.BuildTool.Selection)
                {
                    InteractableObject clickedObj = CheckInteractableRaycast();
                    if (clickedObj != _selectedObject)
                    {
                        if (_selectedObject != null) _selectedObject.SetSelected(false);
                        _selectedObject = clickedObj;
                        if (_selectedObject != null) _selectedObject.SetSelected(true);
                    }
                }
            }
        }
    }
}
