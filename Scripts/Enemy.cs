using Godot;

public partial class Enemy : CharacterBody2D
{
	[Export] public float Speed = 50.0f;
	[Export] public float StopDistance = 20.0f;
	[Export] public float RepathInterval = 0.2f;

	[Export] public float MaxHealth = 30.0f;
	[Export] public float KnockbackForce = 250.0f;
	[Export] public float HitStunDuration = 0.2f;

	[Export] public float AttackDamage = 10.0f;
	[Export] public float AttackCooldown = 0.8f;

	protected Node2D _player;
	protected NavigationAgent2D _navAgent;
	protected AnimatedSprite2D _animatedSprite;
	protected Area2D _hitbox;

	protected float _repathTimer;
	protected float _health;
	protected float _hitTimer;
	protected float _attackTimer;
	protected Vector2 _knockbackVelocity;

	protected bool _isDead = false;
	protected bool _isHit = false;

	public override void _Ready()
	{
		// Everything synchronous happens here, so a subclass can call
		// base._Ready() and safely use these fields right afterwards.
		_health = MaxHealth;

		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		_navAgent = GetNode<NavigationAgent2D>("EnemyNavigationAgent");
		_animatedSprite = GetNode<AnimatedSprite2D>("EnemyAnimatedSprite");
		_hitbox = GetNodeOrNull<Area2D>("DamageArea");

		_navAgent.PathDesiredDistance = 4.0f;
		_navAgent.TargetDesiredDistance = StopDistance;

		_animatedSprite.AnimationFinished += OnAnimationFinished;

		// Only the part that has to wait a physics frame is async.
		SetInitialTarget();
	}

	// Waits one physics frame so the navigation map is ready, then sets the first target.
	protected virtual async void SetInitialTarget()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		if (_player != null && GodotObject.IsInstanceValid(_player))
		{
			_navAgent.TargetPosition = _player.GlobalPosition;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead)
			return;

		// Attack cooldown runs every frame, even while standing still
		if (_attackTimer > 0.0f)
			_attackTimer -= (float)delta;

		// Handle knockback and hit stun
		if (_hitTimer > 0.0f)
		{
			_hitTimer -= (float)delta;

			Velocity = _knockbackVelocity;

			_knockbackVelocity = _knockbackVelocity.MoveToward(
				Vector2.Zero,
				KnockbackForce * 5.0f * (float)delta
			);

			MoveAndSlide();
			return;
		}

		// Find the player again if necessary
		if (_player == null || !GodotObject.IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		// Damage the player if they overlap the hitbox
		TryAttackPlayer();

		// Update navigation target periodically
		_repathTimer -= (float)delta;

		if (_repathTimer <= 0.0f)
		{
			_navAgent.TargetPosition = _player.GlobalPosition;
			_repathTimer = RepathInterval;
		}

		// Stop when the enemy reaches the player
		if (_navAgent.IsNavigationFinished())
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();

			// Keep facing the player while standing next to them
			FaceDirection(_player.GlobalPosition.X - GlobalPosition.X);
			return;
		}

		// Follow the navigation path
		Vector2 nextPoint = _navAgent.GetNextPathPosition();
		Vector2 direction = GlobalPosition.DirectionTo(nextPoint);

		Velocity = direction * Speed;

		MoveAndSlide();

		// Play movement animation only when not playing a hit animation
		if (!_isHit && _animatedSprite.Animation.ToString() != "walk")
		{
			_animatedSprite.Play("walk");
		}

		// Sprite and hitbox face the direction of movement
		FaceDirection(direction.X);
	}

	// Flips the sprite and the hitbox together.
	// Assumes the art faces right by default.
	protected virtual void FaceDirection(float xDirection)
	{
		if (Mathf.IsZeroApprox(xDirection))
			return;

		bool facingLeft = xDirection < 0.0f;

		_animatedSprite.FlipH = facingLeft;
		_hitbox.Scale = new Vector2(facingLeft ? -1.0f : 1.0f, 1.0f);
	}

	protected virtual void TryAttackPlayer()
	{
		if (_attackTimer > 0.0f)
			return;

		foreach (Node2D body in _hitbox.GetOverlappingBodies())
		{
			if (body is Player player)
			{
				player.TakeDamage(AttackDamage);
				_attackTimer = AttackCooldown;
				break;
			}
		}
	}

	// DAMAGE LOGIC SECTION HEREEE
	// Called by the player's attack
	public virtual void TakeDamage(float damage, Vector2 attackerPosition)
	{
		if (_isDead || _health <= 0.0f)
			return;

		_health -= damage;

		GD.Print($"Enemy took {damage} damage! HP: {Mathf.Max(_health, 0.0f)}/{MaxHealth}");

		// Death takes priority over hit animation
		if (_health <= 0.0f)
		{
			_isDead = true;
			_isHit = false;

			Velocity = Vector2.Zero;
			_knockbackVelocity = Vector2.Zero;

			// Disable the hitbox so a dying enemy can't hurt the player
			_hitbox.SetDeferred(Area2D.PropertyName.Monitoring, false);

			_animatedSprite.Play("death");
			return;
		}

		// Calculate knockback direction
		Vector2 knockbackDirection = GlobalPosition - attackerPosition;

		if (knockbackDirection.LengthSquared() > 0.001f)
			knockbackDirection = knockbackDirection.Normalized();
		else
			knockbackDirection = Vector2.Right;

		_knockbackVelocity = knockbackDirection * KnockbackForce;
		_hitTimer = HitStunDuration;
		_isHit = true;

		// Face the attacker
		FaceDirection(attackerPosition.X - GlobalPosition.X);

		// Play the hit animation
		_animatedSprite.Play("hit");
	}

	// Called when a non-looping animation finishes
	protected virtual void OnAnimationFinished()
	{
		if (_isDead)
		{
			if (_animatedSprite.Animation == "death")
			{
				QueueFree();
			}

			return;
		}

		if (_animatedSprite.Animation == "hit")
		{
			_isHit = false;
			_animatedSprite.Play("walk");
		}
	}
}
