using Godot;
using System;

public partial class AimAssist : Node3D
{
    [Export] public NodePath SwingSystemPath;
    private SwingSystem _swingSystem;

    private MeshInstance3D _aimLine;
    private MeshInstance3D _landingMarker;
    private MeshInstance3D _trajectoryArc; // Added

    private Camera3D _camera;
    private bool _isLocked = false;

    public override void _Ready()
    {
        _aimLine = GetNodeOrNull<MeshInstance3D>("AimLine");
        _landingMarker = GetNodeOrNull<MeshInstance3D>("LandingMarker");

        // Create Trajectory Arc mesh if it doesn't exist
        _trajectoryArc = GetNodeOrNull<MeshInstance3D>("TrajectoryArc");
        if (_trajectoryArc == null)
        {
            _trajectoryArc = new MeshInstance3D();
            _trajectoryArc.Name = "TrajectoryArc";
            AddChild(_trajectoryArc);

            var mat = new StandardMaterial3D();
            mat.ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded;
            mat.AlbedoColor = new Color(1, 1, 1, 0.8f);
            mat.Transparency = StandardMaterial3D.TransparencyEnum.Alpha;
            _trajectoryArc.MaterialOverride = mat;
        }

        // Find SwingSystem - Optimized lookup
        if (SwingSystemPath != null && !SwingSystemPath.IsEmpty)
            _swingSystem = GetNodeOrNull<SwingSystem>(SwingSystemPath);

        if (_swingSystem == null)
        {
            // Try common paths and recursive search
            _swingSystem = GetNodeOrNull<SwingSystem>("/root/DrivingRange/SwingSystem");
            if (_swingSystem == null) _swingSystem = GetNodeOrNull<SwingSystem>("/root/TerrainTest/SwingSystem");
            if (_swingSystem == null) _swingSystem = GetTree().Root.FindChild("SwingSystem", true, false) as SwingSystem;
        }

        if (_swingSystem != null)
        {
            GD.Print($"AimAssist: Linked to SwingSystem at {_swingSystem.GetPath()}");
            _swingSystem.ModeChanged -= OnModeChanged;
            _swingSystem.ModeChanged += OnModeChanged;

            _swingSystem.SwingStageChanged -= OnStageChanged;
            _swingSystem.SwingStageChanged += OnStageChanged;

            _swingSystem.ClubChanged -= OnClubChanged;
            _swingSystem.ClubChanged += OnClubChanged;
        }
        else
        {
            GD.PrintErr("AimAssist: FAILED to find SwingSystem in scene tree!");
        }

        // Find Camera
        _camera = GetViewport().GetCamera3D();

        // Default to Hidden (Walk Mode)
        Visible = false;
        SetProcess(false);
    }

    private void OnStageChanged(int newStage)
    {
        // 0 = Idle, 1 = Power, 2 = Accuracy, 3 = Executing, 4 = ShotComplete
        if (newStage == (int)SwingStage.Executing)
        {
            _isLocked = true;
        }
        else if (newStage == (int)SwingStage.Idle)
        {
            _isLocked = false;
            UpdateVisuals();
        }
    }

    private void OnClubChanged(string clubName, float loft, float aoa)
    {
        UpdateVisuals();
    }

    public override void _Process(double delta)
    {
        if (_swingSystem == null || _camera == null) return;

        if (!_isLocked)
        {
            GlobalPosition = _swingSystem.BallPosition;
            Vector3 camRot = _camera.GlobalRotation;
            GlobalRotation = new Vector3(0, camRot.Y, 0);
            UpdateVisuals();
        }
    }

    private void OnModeChanged(bool isGolfing)
    {
        Visible = isGolfing;
        SetProcess(isGolfing);
        GD.Print($"AimAssist: ModeChanged isGolfing={isGolfing}");

        if (isGolfing)
        {
            _camera = GetViewport().GetCamera3D();
            _isLocked = false;
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        if (_swingSystem == null) return;

        // 1. Get Power and smash factor
        float power = _swingSystem.GetEstimatedPower();
        float powerStatMult = power / 10.0f;
        float baseVelocity = Golf.GolfConstants.BASE_VELOCITY;

        BallLie currentLie = _swingSystem.GetCurrentLie();

        var club = _swingSystem.SelectedClub;
        float smashFactor = club != null ? club.PowerMultiplier : 1.0f;
        float headSpeedMult = club != null ? club.HeadSpeedMultiplier : 1.0f;

        // 2. static loft + default AoA
        float staticLoft = club != null ? club.LoftDegrees : 15.0f;
        float effectiveAoA = _swingSystem.AoAOffset;
        if (club != null && Mathf.Abs(_swingSystem.AoAOffset) < 0.01f)
        {
            if (club.Type == ClubType.Driver) effectiveAoA = 3.5f;
            else if (club.Type == ClubType.Iron || club.Type == ClubType.Wedge) effectiveAoA = -2.5f;
        }
        float totalLoft = staticLoft + effectiveAoA;

        // 3. Launch Parameters (MATCHING ShotPhysics.cs)
        float loftRad = Mathf.DegToRad(totalLoft);

        Vector3 camForward = -GetViewport().GetCamera3D().GlobalTransform.Basis.Z;
        Vector3 horizontalDir = new Vector3(camForward.X, 0, camForward.Z).Normalized();

        // Match ShotPhysics direction logic
        Vector3 direction = horizontalDir;
        direction.Y = Mathf.Sin(loftRad) + currentLie.LaunchAngleBonus;
        direction = direction.Normalized();

        float launchPower = baseVelocity * powerStatMult * headSpeedMult * smashFactor * currentLie.PowerEfficiency;

        // Optional: Include overpower if at max stat (similar to ShotPhysics)
        // For prediction, we assume normalizedPower = 1.0 (Full Swing)
        Vector3 initialVelocity = direction * launchPower;

        // Calculate Spin matching ShotPhysics
        float spinMult = club != null ? club.SpinMultiplier : 1.0f;
        float loftSpinBonus = 1.0f + (totalLoft / 45.0f);

        // Match ShotPhysics baselineBackspin formula
        float totalBackspin = 380.0f * (1.0f * powerStatMult) * currentLie.SpinModifier * spinMult * loftSpinBonus;

        Vector3 rightDir = horizontalDir.Cross(Vector3.Up).Normalized();
        Vector3 spin = rightDir * totalBackspin;

        // 4. Perform Simulation
        var points = SimulateFlight(initialVelocity, spin);

        if (points.Count > 0)
        {
            Vector3 landingPoint = points[points.Count - 1];
            float predictedMeters = new Vector2(landingPoint.X, landingPoint.Z).Length();
            float reportedDistance = predictedMeters * Golf.GolfConstants.UNIT_RATIO;

            if (Engine.GetFramesDrawn() % 60 == 0)
            {
                GD.Print($"AimAssist: Club={club?.ClubName}, Loft={totalLoft}, PowerStat={powerStatMult}, LaunchV={initialVelocity.Length():F1}m/s, Spin={totalBackspin:F0}");
                GD.Print($"AimAssist: TravelTime={points.Count * 0.0166f:F2}s, PredDist={predictedMeters:F1}m -> {reportedDistance:F1}y");
            }

            if (_aimLine != null)
            {
                _aimLine.Scale = new Vector3(_aimLine.Scale.X, _aimLine.Scale.Y, predictedMeters);
                _aimLine.Position = new Vector3(0, 0, -predictedMeters / 2.0f);

                var mat = _aimLine.GetActiveMaterial(0) as StandardMaterial3D;
                if (mat != null)
                {
                    if (club?.Type == ClubType.Wedge) mat.AlbedoColor = new Color(0, 1, 0.5f, 0.5f);
                    else if (club?.Type == ClubType.Driver) mat.AlbedoColor = new Color(1, 0.5f, 0, 0.5f);
                    else mat.AlbedoColor = new Color(0, 0.5f, 1, 0.5f);
                }
            }

            if (_landingMarker != null)
            {
                _landingMarker.Position = new Vector3(0, 0, -predictedMeters);
            }

            DrawTrajectoryArc(points);
        }
    }

    private System.Collections.Generic.List<Vector3> SimulateFlight(Vector3 velocity, Vector3 spin)
    {
        var points = new System.Collections.Generic.List<Vector3>();
        Vector3 pos = Vector3.Zero;
        Vector3 currentVel = velocity;
        float dt = 1.0f / 60.0f; // Match physics tick
        float maxTime = 12f; // Failsafe

        points.Add(pos);

        for (float t = 0; t < maxTime; t += dt)
        {
            float speed = currentVel.Length();
            if (speed < 0.1f) break;

            // Drag
            float dragMag = 0.5f * Golf.GolfConstants.DRAG_COEFFICIENT * Golf.GolfConstants.AIR_DENSITY *
                            Golf.GolfConstants.BALL_AREA * (speed * speed);
            Vector3 dragForce = -currentVel.Normalized() * dragMag;

            // Lift (Magnus)
            float spinSpeed = spin.Length();
            float spinParam = (Golf.GolfConstants.BALL_RADIUS * spinSpeed) / Mathf.Max(speed, 0.1f);
            float Cl = Golf.GolfConstants.LIFT_COEFFICIENT * (1.0f + spinParam);
            Vector3 liftDir = spin.Cross(currentVel).Normalized();
            float liftMag = 0.5f * Cl * Golf.GolfConstants.AIR_DENSITY * Golf.GolfConstants.BALL_AREA * (speed * speed);
            Vector3 liftForce = liftDir * liftMag;

            // Total Force
            Vector3 accel = (dragForce + liftForce) / Golf.GolfConstants.BALL_MASS;
            accel += Vector3.Down * Golf.GolfConstants.GRAVITY;

            // Update state
            currentVel += accel * dt;
            pos += currentVel * dt;

            points.Add(pos);

            // Ground hit check (Only after a bit of flight)
            if (pos.Y < -0.05f && t > 0.1f) break;
        }

        return points;
    }

    private void DrawTrajectoryArc(System.Collections.Generic.List<Vector3> points)
    {
        if (_trajectoryArc == null) return;

        var imm = new ImmediateMesh();
        _trajectoryArc.Mesh = imm;

        imm.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        foreach (var p in points)
        {
            // The simulation pos is relative to launch. 
            // In the world, we are rotated. But UpdateVisuals 
            // set our GlobalRotation to match camera. 
            // So we just draw relative to local Z.
            float horizontalDist = new Vector2(p.X, p.Z).Length();
            imm.SurfaceAddVertex(new Vector3(0, p.Y, -horizontalDist));
        }
        imm.SurfaceEnd();
    }
}
