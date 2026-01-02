using Godot;
using System;

public partial class GolfCart : CharacterBody3D
{
	[Export] public float MaxSpeed = 15.0f;
	[Export] public float Acceleration = 10.0f;
	[Export] public float SteeringSpeed = 2.0f;
	[Export] public float BrakeForce = 20.0f;
	[Export] public float Gravity = 9.8f;

	private float _currentSpeed = 0.0f;
	private float _steeringAngle = 0.0f;
	private bool _isBeingDriven = false;
	private PlayerController _driver;

	public bool IsBeingDriven => _isBeingDriven;

	public void Enter(PlayerController player)
	{
		_isBeingDriven = true;
		_driver = player;
		GD.Print("Player entered the cart.");
	}

	public void Exit()
	{
		_isBeingDriven = false;
		_driver = null;
		_currentSpeed = 0;
		GD.Print("Player exited the cart.");
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Apply Gravity
		if (!IsOnFloor())
		{
			velocity.Y -= Gravity * (float)delta;
		}
		else
		{
			velocity.Y = 0;
		}

		if (_isBeingDriven)
		{
			HandleDriving((float)delta, ref velocity);
		}
		else
		{
			// Simple friction when not driven
			_currentSpeed = Mathf.MoveToward(_currentSpeed, 0, BrakeForce * 0.5f * (float)delta);
			velocity.X = Transform.Basis.Z.X * _currentSpeed;
			velocity.Z = Transform.Basis.Z.Z * _currentSpeed;
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	private void HandleDriving(float delta, ref Vector3 velocity)
	{
		float inputForward = 0.0f;
		if (Input.IsKeyPressed(Key.W)) inputForward += 1.0f;
		if (Input.IsKeyPressed(Key.S)) inputForward -= 1.0f;

		float inputSteer = 0.0f;
		if (Input.IsKeyPressed(Key.A)) inputSteer += 1.0f;
		if (Input.IsKeyPressed(Key.D)) inputSteer -= 1.0f;

		// Acceleration / Braking
		if (Mathf.Abs(inputForward) > 0.1f)
		{
			_currentSpeed = Mathf.MoveToward(_currentSpeed, inputForward * MaxSpeed, Acceleration * delta);
		}
		else
		{
			_currentSpeed = Mathf.MoveToward(_currentSpeed, 0, BrakeForce * 0.5f * delta);
		}

		// Steering (Only when moving)
		if (Mathf.Abs(_currentSpeed) > 0.1f)
		{
			float direction = _currentSpeed > 0 ? 1.0f : -1.0f;
			RotateY(inputSteer * SteeringSpeed * delta * direction);
		}

		// Move in Forward Direction (Basis Z is usually forward in Godot, but let's check orientation)
		// In the DrivingRange.tscn, the cart seems to face +Z or -Z. 
		// Let's assume Transform.Basis.Z is the forward/back axis.
		velocity.X = Transform.Basis.Z.X * _currentSpeed;
		velocity.Z = Transform.Basis.Z.Z * _currentSpeed;
	}
}
