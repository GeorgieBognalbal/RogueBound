
using Godot;

public abstract partial class Enemy : CharacterBody2D
{
	[Export] public float Speed = 50.0f;
	[Export] public float StopDistance = 20.0f;
	[Export] public float RepathInterval = 0.2f;

	[Export] public float MaxHealth = 30.0f;
	[Export] public float KnockbackForce = 250.0f;
	[Export] public float HitStunDuration = 0.2f;

	[Export] public float AttackDamage = 10.0f;
	[Export] public float AttackCooldown = 0.8f;

	// Scene used to display floating damage numbers.
	[Export] public PackedScene DamageNumberScene;

	[Export] public PackedScene CoinDropScene;
	[Export] public PackedScene HealthDropScene;
	[Export] public float HealthDropChance = 0.25f;

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
		AddToGroup("enemies");

		_health = MaxHealth;

		// Get the objects in the scene tree.
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		_navAgent = GetNode<NavigationAgent2D>("EnemyNavigationAgent");
		_animatedSprite = GetNode<AnimatedSprite2D>("EnemyAnimatedSprite");
		_hitbox = GetNodeOrNull<Area2D>("DamageArea");

		if (_hitbox == null)
		{
			GD.PushError($"{Name}: Missing Area2D named DamageArea.");
		}
		else
		{
			// Players use collision layer 2. Set this explicitly instead of relying
			// on Area2D's default mask (layer 1), which never sees the player.
			_hitbox.CollisionLayer = 0;
			_hitbox.CollisionMask = 1u << 1;
		}

		_navAgent.PathDesiredDistance = 4.0f;
		_navAgent.TargetDesiredDistance = StopDistance;

		_animatedSprite.AnimationFinished += OnAnimationFinished;

		SetInitialTarget();
	}

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

		if (_attackTimer > 0.0f)
			_attackTimer -= (float)delta;

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

		if (_player == null || !GodotObject.IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		TryAttackPlayer();

		_repathTimer -= (float)delta;

		if (_repathTimer <= 0.0f)
		{
			_navAgent.TargetPosition = _player.GlobalPosition;
			_repathTimer = RepathInterval;
		}

		if (_navAgent.IsNavigationFinished())
		{
			// A navigation query can briefly report a finished path before the
			// server has produced a path, or when the target is just off the map.
			// Keep pursuing until the enemy reaches its own stopping distance.
			float distanceToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
			if (distanceToPlayer > _navAgent.TargetDesiredDistance + _navAgent.PathDesiredDistance)
			{
				Vector2 fallbackDirection = GlobalPosition.DirectionTo(_player.GlobalPosition);
				Velocity = fallbackDirection * Speed;
				MoveAndSlide();
				FaceDirection(fallbackDirection.X);
				if (!_isHit && _animatedSprite.Animation.ToString() != "walk")
					_animatedSprite.Play("walk");
				return;
			}

			Velocity = Vector2.Zero;
			MoveAndSlide();
			FaceDirection(_player.GlobalPosition.X - GlobalPosition.X);
			return;
		}

		Vector2 nextPoint = _navAgent.GetNextPathPosition();
		Vector2 direction = GlobalPosition.DirectionTo(nextPoint);

		Velocity = direction * Speed;

		MoveAndSlide();

		if (!_isHit && _animatedSprite.Animation.ToString() != "walk")
		{
			_animatedSprite.Play("walk");
		}

		FaceDirection(direction.X);
	}

	protected virtual void FaceDirection(float xDirection)
	{
		if (Mathf.IsZeroApprox(xDirection))
			return;

		bool facingLeft = xDirection < 0.0f;

		_animatedSprite.FlipH = facingLeft;

	}

	protected virtual void TryAttackPlayer()
	{
		if (_attackTimer > 0.0f || _hitbox == null)
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

	// Spawns a floating number showing damage dealt to this enemy.
	protected virtual void SpawnDamageNumber(float damage)
	{
		PackedScene damageScene = DamageNumberScene
			?? GD.Load<PackedScene>("res://Scenes/Effects/DamageNumber.tscn");
		if (damageScene == null)
			return;

		Node currentScene = GetTree().CurrentScene;

		if (currentScene == null)
			return;

		Node2D damageNumber = damageScene.Instantiate<Node2D>();
		currentScene.AddChild(damageNumber);

		damageNumber.GlobalPosition =
			GlobalPosition + new Vector2(0.0f, -20.0f);

		damageNumber.Call("ShowDamage", damage);
	}

	// Called by the player's attack.
	public virtual void TakeDamage(float damage, Vector2 attackerPosition)
	{
		if (_isDead || _health <= 0.0f || damage <= 0.0f)
			return;

		_health -= damage;
		CombatEffects.Burst(this, CombatEffects.Impact, direction: attackerPosition.DirectionTo(GlobalPosition));
		if (GodotObject.IsInstanceValid(_player) && _player is Player player)
			player.ShakeCamera(0.85f);

		// Play hit sound if available.
		GetNodeOrNull<AudioStreamPlayer2D>("HitBoxSound")?.Play();

		// Display damage number.
		SpawnDamageNumber(damage);

		GD.Print(
			$"Enemy took {damage} damage! " +
			$"HP: {Mathf.Max(_health, 0.0f)}/{MaxHealth}"
		);

		// Check if enemy died.
		if (_health <= 0.0f)
		{
			_isDead = true;

			// RECORD ENEMY KILL IN SQLITE DATABASE.
			DatabaseManager database =
				GetNode<DatabaseManager>("/root/DatabaseManager");

			string enemyType = GetType().Name.Replace("Enemy", "");

			database.RecordEnemyKill(enemyType);

			GD.Print($"Database recorded kill: {enemyType}");

			_isHit = false;

			Velocity = Vector2.Zero;
			_knockbackVelocity = Vector2.Zero;

			if (_hitbox != null)
			{
				_hitbox.SetDeferred(
					Area2D.PropertyName.Monitoring,
					false
				);
			}

			// Play death sound if available.
			GetNodeOrNull<AudioStreamPlayer2D>("EnemyKill")?.Play();

			SpawnDrops();

			_animatedSprite.Play("death");
			return;
		}

		ApplyHitReaction(attackerPosition);
	}

	protected virtual void ApplyHitReaction(Vector2 attackerPosition)
	{
		// Calculate knockback direction.
		Vector2 knockbackDirection = GlobalPosition - attackerPosition;

		if (knockbackDirection.LengthSquared() > 0.001f)
			knockbackDirection = knockbackDirection.Normalized();
		else
			knockbackDirection = Vector2.Right;

		_knockbackVelocity = knockbackDirection * KnockbackForce;
		_hitTimer = HitStunDuration;
		_isHit = true;

		FaceDirection(attackerPosition.X - GlobalPosition.X);

		_animatedSprite.Play("hit");
	}

	// Added for WaveManager: identifies enemies that have died.
	public bool IsDead()
	{
		return _isDead;
	}

	// Added for convenient health checks.
	public float GetHealth()
	{
		return _health;
	}

	protected virtual void SpawnDrops()
	{
		var coinScene = CoinDropScene ?? GD.Load<PackedScene>("res://Scenes/Items/CoinPickup.tscn");
		if (coinScene != null && GetTree()?.CurrentScene != null)
		{
			Node2D coin = coinScene.Instantiate<Node2D>();
			if (coin is Pickup coinPickup)
			{
				var dungeon = GetTree().CurrentScene.GetNodeOrNull<RogueBound.Dungeon.DungeonManager>("DungeonManager");
				coinPickup.Value = 5 + Mathf.Max(0, (dungeon?.CurrentFloor ?? 1) - 1) * 2;
			}
			coin.GlobalPosition = GlobalPosition;
			GetTree().CurrentScene.CallDeferred("add_child", coin);
		}

		if (GD.Randf() < HealthDropChance)
		{
			var potScene = HealthDropScene ?? GD.Load<PackedScene>("res://Scenes/Items/HealthPickup.tscn");
			if (potScene != null && GetTree()?.CurrentScene != null)
			{
				Node2D pot = potScene.Instantiate<Node2D>();
				pot.GlobalPosition = GlobalPosition + new Vector2(
					(float)GD.RandRange(-10.0, 10.0),
					(float)GD.RandRange(-10.0, 10.0)
				);
				GetTree().CurrentScene.CallDeferred("add_child", pot);
			}
		}
	}

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
