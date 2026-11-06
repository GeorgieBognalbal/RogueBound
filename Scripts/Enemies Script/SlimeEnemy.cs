
using Godot;

public partial class SlimeEnemy : Enemy
{
	[Export] public float ChargeRange = 180.0f;
	[Export] public float ChargeDuration = 0.8f;
	[Export] public float DashSpeed = 250.0f;
	[Export] public float DashDuration = 0.45f;
	[Export] public float DashCooldown = 2.0f;

	private enum SlimeState
	{
		Chasing,
		Charging,
		Dashing
	}

	private SlimeState _state = SlimeState.Chasing;

	private float _stateTimer = 0.0f;
	private float _dashCooldownTimer = 0.0f;

	private Vector2 _dashDirection = Vector2.Zero;

	public override void _Ready()
	{
		base._Ready();

		// The slime can navigate until it gets close enough to charge.
		_navAgent.TargetDesiredDistance = ChargeRange;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead)
			return;

		float dt = (float)delta;

		if (_attackTimer > 0.0f)
			_attackTimer -= dt;

		if (_dashCooldownTimer > 0.0f)
			_dashCooldownTimer -= dt;

		// Preserve the base enemy's hit-stun and knockback behavior.
		if (_hitTimer > 0.0f)
		{
			_hitTimer -= dt;

			Velocity = _knockbackVelocity;

			_knockbackVelocity = _knockbackVelocity.MoveToward(
				Vector2.Zero,
				KnockbackForce * 5.0f * dt
			);

			MoveAndSlide();
			return;
		}

		// Find the player if necessary.
		if (_player == null || !GodotObject.IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		switch (_state)
		{
			case SlimeState.Chasing:
				ChasePlayer(dt);
				break;

			case SlimeState.Charging:
				ChargePlayer(dt);
				break;

			case SlimeState.Dashing:
				DashPlayer(dt);
				break;
		}
	}

	private void ChasePlayer(float delta)
	{
		TryAttackPlayer();

		_repathTimer -= delta;

		if (_repathTimer <= 0.0f)
		{
			_navAgent.TargetPosition = _player.GlobalPosition;
			_repathTimer = RepathInterval;
		}

		float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);

		// Start charging when the player is close enough.
		if (distance <= ChargeRange && _dashCooldownTimer <= 0.0f)
		{
			StartCharging();
			return;
		}

		if (_navAgent.IsNavigationFinished())
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();

			FaceDirection(_player.GlobalPosition.X - GlobalPosition.X);
			_animatedSprite.Play("idle");
			return;
		}

		Vector2 nextPoint = _navAgent.GetNextPathPosition();
		Vector2 direction = GlobalPosition.DirectionTo(nextPoint);

		Velocity = direction * Speed;

		MoveAndSlide();

		FaceDirection(direction.X);

		if (_animatedSprite.Animation.ToString() != "walk")
			_animatedSprite.Play("walk");
	}

	private void StartCharging()
	{
		_state = SlimeState.Charging;
		_stateTimer = ChargeDuration;

		// Remember the player's position when charging begins.
		_dashDirection = GlobalPosition.DirectionTo(
			_player.GlobalPosition
		);

		FaceDirection(_dashDirection.X);

		Velocity = Vector2.Zero;

		//_animatedSprite.Play("charge");
	}

	private void ChargePlayer(float delta)
	{
		_stateTimer -= delta;

		Velocity = Vector2.Zero;
		MoveAndSlide();

		if (_player != null && GodotObject.IsInstanceValid(_player))
			FaceDirection(_dashDirection.X);

		if (_stateTimer <= 0.0f)
		{
			StartDashing();
		}
	}

	private void StartDashing()
	{
		_state = SlimeState.Dashing;
		_stateTimer = DashDuration;

		_dashCooldownTimer = DashCooldown;

		Velocity = _dashDirection * DashSpeed;

		//_animatedSprite.Play("dash");
	}

	private void DashPlayer(float delta)
	{
		_stateTimer -= delta;

		Velocity = _dashDirection * DashSpeed;

		MoveAndSlide();

		// Damage the player if the slime's DamageArea overlaps them.
		TryAttackPlayer();

		// Stop if the dash duration expires or the slime hits a wall.
		if (_stateTimer <= 0.0f || IsOnWall())
		{
			FinishDashing();
		}
	}

	private void FinishDashing()
	{
		_state = SlimeState.Chasing;

		Velocity = Vector2.Zero;

		_animatedSprite.Play("idle");

		// Update navigation after the dash.
		if (_player != null && GodotObject.IsInstanceValid(_player))
			_navAgent.TargetPosition = _player.GlobalPosition;
	}
}
