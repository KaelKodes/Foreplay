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

        // Get launch parameters similar to ShotPhysics
        float power = _swingSystem.GetEstimatedPower();
        float powerStatMult = power / 10.0f;
        float baseVelocity = Golf.GolfConstants.BASE_VELOCITY;

        var club = _swingSystem.SelectedClub;
        float clubPowerMult = club != null ? club.PowerMultiplier : 1.0f;
        float loft = club != null ? club.LoftDegrees : 15.0f;
        loft += _swingSystem.AoAOffset;

        // Simple physics prediction
        float launchPower = baseVelocity * powerStatMult * clubPowerMult;
        float loftRad = Mathf.DegToRad(loft);

        float vx = launchPower * Mathf.Cos(loftRad);
        float vy = launchPower * Mathf.Sin(loftRad);
        float g = Golf.GolfConstants.GRAVITY;

        // Time to hit ground (y=0) -> 0 = vy*t - 0.5*g*t^2 -> t(vy - 0.5*g*t) = 0 -> t = 2*vy/g
        float timeTotal = (2.0f * vy) / g;

        // --- SCALE FIXES (LOCKED) ---
        float liftFactor = Golf.GolfConstants.LIFT_FACTOR;
        timeTotal *= liftFactor;

        float unitRatio = Golf.GolfConstants.UNIT_RATIO;

        float predictedMeters = vx * timeTotal;
        float reportedDistance = predictedMeters * unitRatio;

        if (Engine.GetFramesDrawn() % 60 == 0)
            GD.Print($"AimAssist: Club={club?.ClubName}, Loft={loft}, PredictedYards={reportedDistance}");

        if (_aimLine != null)
        {
            _aimLine.Scale = new Vector3(_aimLine.Scale.X, _aimLine.Scale.Y, predictedMeters);
            _aimLine.Position = new Vector3(0, 0, -predictedMeters / 2.0f);

            // Tint line based on club type
            var mat = _aimLine.GetActiveMaterial(0) as StandardMaterial3D;
            if (mat != null)
            {
                if (club?.Type == ClubType.Wedge) mat.AlbedoColor = new Color(0, 1, 0.5f, 0.5f); // Greenish
                else if (club?.Type == ClubType.Driver) mat.AlbedoColor = new Color(1, 0.5f, 0, 0.5f); // Orange
                else mat.AlbedoColor = new Color(0, 0.5f, 1, 0.5f); // Blue
            }
        }

        if (_landingMarker != null)
        {
            _landingMarker.Position = new Vector3(0, 0, -predictedMeters);
        }

        DrawTrajectoryArc(vx, vy, timeTotal, liftFactor);
    }

    private void DrawTrajectoryArc(float vx, float vy, float timeTotal, float liftFactor)
    {
        if (_trajectoryArc == null) return;

        var imm = new ImmediateMesh();
        _trajectoryArc.Mesh = imm;

        imm.SurfaceBegin(Mesh.PrimitiveType.LineStrip);

        int segments = 24;
        float g = 9.8f;

        for (int i = 0; i <= segments; i++)
        {
            float t = (timeTotal * i) / segments;

            // Adjust time for gravity calc to offset the lift factor
            float physicsT = t / liftFactor;

            float x = vx * t; // Matching predictedMeters (dragHeuristic = 1.0)
            float y = (vy * physicsT) - (0.5f * g * physicsT * physicsT);

            if (y < -0.1f && i > 0) break;

            imm.SurfaceAddVertex(new Vector3(0, y, -x));
        }

        imm.SurfaceEnd();
    }
}
