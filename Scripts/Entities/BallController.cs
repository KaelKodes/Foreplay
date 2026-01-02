using Godot;
using System;

public enum BallState
{
    Ready,
    InFlight,
    Rolling,
    Resting
}

public partial class BallController : RigidBody3D
{
    // Multiplayer Ownership (0 = Server/Unassigned, >0 = PeerID)
    public int OwnerId { get; set; } = 0;

    [Export] public float DragCoefficient = 0.18f; // Increased for better wind interaction
    [Export] public float LiftCoefficient = 0.15f; // Reduced to balance against wind
    [Export] public float AirDensity = 1.225f;
    [Export] public float BallArea = 0.001432f;
    [Export] public float BallRadius = 0.021f;
    [Export] public float BallMass = 0.045f;
    [Export] public float WindMultiplier = 1.0f; // Reset to 1.0 baseline with realistic drag

    // ...

    // Physics State
    private Vector3 _windVelocity = Vector3.Zero;
    private Vector3 _spinVector = Vector3.Zero;

    private BallState _state = BallState.Ready;
    private Vector3 _launchPos = Vector3.Zero;
    private bool _hasCarried = false;

    [Signal] public delegate void BallSettledEventHandler(float totalDistance);
    [Signal] public delegate void BallCarriedEventHandler(float carryDistance);

    private float _settleTimer = 0.0f;
    private float _flightTimer = 0.0f;
    private const float SETTLE_THRESHOLD = 0.4f; // m/s
    private const float SETTLE_TIME = 0.8f;
    private const float MIN_FLIGHT_TIME = 0.2f;
    private const float METERS_TO_YARDS = 1.09361f;

    public override void _Ready()
    {
        CustomIntegrator = true;
        Mass = BallMass;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_state == BallState.Resting) return;

        if (_state == BallState.InFlight || _state == BallState.Rolling)
        {
            _flightTimer += (float)delta;

            float currentSpeed = LinearVelocity.Length();

            // Settlement check
            if (currentSpeed < SETTLE_THRESHOLD && _flightTimer > MIN_FLIGHT_TIME)
            {
                _settleTimer += (float)delta;
                if (_settleTimer >= SETTLE_TIME)
                {
                    StopBall();
                }
            }
            else
            {
                _settleTimer = 0.0f;
            }

            // Failsafe: Reset if falling out of world
            if (GlobalPosition.Y < -20.0f)
            {
                StopBall();
            }
        }
    }

    private void StopBall()
    {
        _state = BallState.Resting;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        _settleTimer = 0.0f;
        Freeze = true;
        float total = _launchPos.DistanceTo(new Vector3(GlobalPosition.X, _launchPos.Y, GlobalPosition.Z));
        EmitSignal(SignalName.BallSettled, total * 2.0f); // Restore 2.0x Visual Scale
    }

    public void Launch(Vector3 velocity, Vector3 spin)
    {
        Freeze = false; // Unfreeze FIRST for Godot 4 stability
        LinearVelocity = velocity;
        AngularVelocity = spin;
        _spinVector = spin;
        _state = BallState.InFlight;
        _launchPos = GlobalPosition;
        _hasCarried = false;
        _flightTimer = 0.0f;
        _settleTimer = 0.0f;
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float dt = (float)state.Step;
        Vector3 pos = state.Transform.Origin;

        // 1. Ground Detection (Raycast)
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(pos, pos + Vector3.Down * (BallRadius + 0.05f));
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        var result = spaceState.IntersectRay(query);

        bool onGround = result.Count > 0;

        // "Takeoff" Rule: If moving up significantly, ignore ground (prevents launch friction)
        if (state.LinearVelocity.Y > 0.1f) onGround = false;

        // Grace period: ignore ground detection for the first few frames of flight
        if (_state == BallState.InFlight && _flightTimer < 0.08f) // ~5 frames at 60 Hz
        {
            onGround = false;
        }


        // Aerodynamics (LIFT only airborne, DRAG always)
        ApplyAerodynamics(state, !onGround);

        // ALWAYS apply gravity if custom integrator is on
        state.LinearVelocity += state.TotalGravity * dt;

        if (onGround)
        {
            // First impact (Carry)
            if (!_hasCarried && _flightTimer > MIN_FLIGHT_TIME)
            {
                _hasCarried = true;
                float carry = _launchPos.DistanceTo(new Vector3(pos.X, _launchPos.Y, pos.Z));
                EmitSignal(SignalName.BallCarried, carry * 2.0f); // Restore 2.0x Visual Scale
                _state = BallState.Rolling;
            }

            // Delay rolling friction until the ball has been airborne for a short time
            if (_flightTimer > 0.05f)
            {
                ApplyRollingFriction(state, result);
            }
        }
    }

    private void ApplyRollingFriction(PhysicsDirectBodyState3D state, Godot.Collections.Dictionary result)
    {
        float dt = (float)state.Step;
        float resistance = 4.0f; // Base deceleration (m/s^2)

        Node collider = (Node)result["collider"];
        if (collider != null)
        {
            string cName = collider.Name.ToString().ToLower();

            // 1. Check for Surveyed Terrain
            if (collider is SurveyedTerrain terrain)
            {
                switch (terrain.TerrainType)
                {
                    case 0: resistance = 4.5f; break;   // Fairway
                    case 1: resistance = 15.0f; break;  // Rough
                    case 2: resistance = 35.0f; break;  // Deep Rough
                    case 3: resistance = 2.5f; break;   // Green
                    case 4: resistance = 150.0f; break; // Sand (Instant stop)
                    case 5: // Water
                        StopBall();
                        GD.Print("Ball landed in Water! Water Hazard.");
                        return;
                }
            }
            // 2. Fallback for default grounds
            else if (cName.Contains("rough"))
            {
                resistance = 12.0f;
            }
            else if (cName.Contains("green"))
            {
                resistance = 2.5f;
            }
            else if (cName.Contains("fairway"))
            {
                resistance = 4.5f;
            }
        }

        Vector3 vel = state.LinearVelocity;
        float speed = vel.Length();

        if (speed > 0.01f)
        {
            // Multi-pronged friction
            // 1. Percentage (High-speed drag)
            state.LinearVelocity *= (1.0f - (resistance * 0.5f * dt));

            // 2. Absolute (Low-speed braking)
            Vector3 brakeDir = vel.Normalized();
            Vector3 brakeForce = brakeDir * resistance * dt;

            if (brakeForce.Length() > state.LinearVelocity.Length())
                state.LinearVelocity = Vector3.Zero;
            else
                state.LinearVelocity -= brakeForce;
        }
        else
        {
            state.LinearVelocity = Vector3.Zero;
        }

        // Kill spin quickly on ground
        state.AngularVelocity *= (1.0f - 15.0f * dt);
        if (state.AngularVelocity.Length() < 0.5f) state.AngularVelocity = Vector3.Zero;

        // Push slightly out of ground to prevent clipping/friction fighting
        if (_state != BallState.InFlight) _spinVector = Vector3.Zero; // Reset aerodynamics spin while on ground
    }

    public void SetWind(Vector3 windVel)
    {
        _windVelocity = windVel;
    }

    private void ApplyAerodynamics(PhysicsDirectBodyState3D state, bool isAirborne)
    {
        // Prevent wind from blowing ball off tee or when resting
        if (_state == BallState.Ready || _state == BallState.Resting) return;

        // Aerodynamics based on Relative Velocity (Ball - Wind)
        // Amplify wind effect ONLY when airborne to prevent ground rolling
        Vector3 windEffect = isAirborne ? (_windVelocity * WindMultiplier) : Vector3.Zero;

        Vector3 relativeVelocity = state.LinearVelocity - windEffect;
        float speed = relativeVelocity.Length();
        if (speed < 1.0f) return;

        float dt = (float)state.Step;
        Vector3 relVelDir = relativeVelocity.Normalized();

        // 1. Drag: Always applies
        float dragMag = 0.5f * DragCoefficient * AirDensity * BallArea * (speed * speed);
        Vector3 dragForce = -relVelDir * dragMag;

        // 2. Lift (Magnus): Only while airborne
        Vector3 liftForce = Vector3.Zero;
        if (isAirborne)
        {
            float spinSpeed = _spinVector.Length();
            float spinParam = (BallRadius * spinSpeed) / speed;
            float Cl = LiftCoefficient * (1.0f + spinParam);

            Vector3 liftDir = _spinVector.Cross(relativeVelocity).Normalized();
            float liftMag = 0.5f * Cl * AirDensity * BallArea * (speed * speed);
            liftForce = liftDir * liftMag;
        }

        Vector3 totalForce = dragForce + liftForce;
        state.LinearVelocity += (totalForce / BallMass) * dt;
    }

    public void PrepareNextShot()
    {
        _state = BallState.Ready;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        _spinVector = Vector3.Zero;
        // Do NOT reset GlobalPosition
        Freeze = false;
        _hasCarried = false;
        _flightTimer = 0.0f;
        _settleTimer = 0.0f;
    }

    public void Reset()
    {
        _state = BallState.Ready;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        _spinVector = Vector3.Zero;
        GlobalPosition = new Vector3(49.51819f, 1.0f, -59.909077f);
        Freeze = false; // Let it fall/settle naturally like first load!
        _hasCarried = false;
        _flightTimer = 0.0f;
        _settleTimer = 0.0f;
    }
}
