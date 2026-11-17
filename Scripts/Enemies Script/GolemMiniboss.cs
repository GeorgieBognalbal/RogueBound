using Godot;

public partial class GolemMiniboss : Enemy
{
	private enum BossAction { Idle, Spawning, RockBarrage, Boulder, Dying }

	[Export] public PackedScene RockScene;
	[Export] public float AttackRange = 250.0f;
	[Export] public float RockBarrageDuration = 3.6f;
	[Export] public float RockBarrageCooldown = 4.5f;
	[Export] public float BoulderCooldown = 3.5f;
	[Export] public float SmallRockDamage = 12.0f;
	[Export] public float BoulderDamage = 24.0f;
	[Export] public float SmallRockSpeed = 105.0f;
	[Export] public float BoulderSpeed = 125.0f;

	private BossAction _action = BossAction.Spawning;
	private CollisionShape2D _bodyShape;
	private float _actionTimer;
	private int _lastAttackFrame = -1;
	private bool _boulderLaunched;
	private bool _nextAttackIsBoulder;

	public override void _Ready()
	{
		// A slow, durable enemy; floor scaling is applied by DungeonRoom before _Ready.
		Speed = 18.0f;
		StopDistance = 150.0f;
		AttackDamage = Mathf.Max(AttackDamage, 18.0f);
		AttackCooldown = 2.0f;
		MaxHealth = Mathf.Max(MaxHealth, 420.0f);
		base._Ready();

		_bodyShape = GetNodeOrNull<CollisionShape2D>("EnemyCollisionShape");
		if (_hitbox != null)
			_hitbox.Monitoring = false;
		_action = BossAction.Spawning;
		_animatedSprite.Play("spawn");
	}

	public override void _Process(double delta)
	{
		if (_action == BossAction.RockBarrage)
		{
			int frame = _animatedSprite.Frame;
			if (frame != _lastAttackFrame)
			{
				_lastAttackFrame = frame;
				// Godot frame 0/2 are the sheet's first and third (left/right throw) frames.
				if (frame == 0 || frame == 2)
					ThrowSmallRock(frame == 0 ? -1 : 1);
			}
		}
		else if (_action == BossAction.Boulder && !_boulderLaunched && _animatedSprite.Frame == 3)
		{
			// Sheet frame 4 is zero-based frame 3: the boulder release pose.
			_boulderLaunched = true;
			ThrowBoulder();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_action == BossAction.Spawning || _action == BossAction.Dying || _isDead)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		if (_action == BossAction.RockBarrage)
		{
			_attackTimer = Mathf.Max(0.0f, _attackTimer - dt);
			_actionTimer -= dt;
			Velocity = Vector2.Zero;
			MoveAndSlide();
			if (_actionTimer <= 0.0f)
				ReturnToIdle();
			return;
		}

		if (_action == BossAction.Boulder)
		{
			_attackTimer = Mathf.Max(0.0f, _attackTimer - dt);
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		// Let the shared Enemy implementation handle knockback while a hit animation plays.
		if (_hitTimer > 0.0f || _isHit)
		{
			base._PhysicsProcess(delta);
			return;
		}

		if (_player != null && GodotObject.IsInstanceValid(_player) && _attackTimer <= 0.0f &&
			GlobalPosition.DistanceTo(_player.GlobalPosition) <= AttackRange)
		{
			StartNextAttack();
			return;
		}

		Vector2 before = GlobalPosition;
		base._PhysicsProcess(delta);
		if (_isDead || _isHit || _action != BossAction.Idle)
			return;

		bool moved = GlobalPosition.DistanceSquaredTo(before) > 0.01f;
		string wantedAnimation = moved ? "walk" : "idle";
		if (_animatedSprite.Animation.ToString() != wantedAnimation)
			_animatedSprite.Play(wantedAnimation);
	}

	protected override void TryAttackPlayer()
	{
		// Attacks are started by the boss state machine above, never by contact damage.
	}

	public override void TakeDamage(float damage, Vector2 attackerPosition)
	{
		if (_action == BossAction.Spawning || _action == BossAction.Dying || _isDead)
			return;

		_action = BossAction.Idle;
		base.TakeDamage(damage, attackerPosition);
		if (_isDead)
		{
			_action = BossAction.Dying;
			_attackTimer = 0.0f;
			_animatedSprite.SelfModulate = Colors.White;
			CollisionLayer = 0;
			if (_hitbox != null)
				_hitbox.SetDeferred(Area2D.PropertyName.Monitoring, false);
			if (_bodyShape != null)
				_bodyShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		}
		else
		{
			_animatedSprite.SelfModulate = new Color(1.0f, 0.65f, 0.65f, 1.0f);
		}
	}

	protected override void OnAnimationFinished()
	{
		if (_action == BossAction.Dying && _animatedSprite.Animation == "death")
		{
			// Hold the final rubble frame for a moment before cleaning up.
			GetTree().CreateTimer(3.0).Timeout += QueueFree;
			return;
		}

		if (_action == BossAction.Spawning && _animatedSprite.Animation == "spawn")
		{
			_action = BossAction.Idle;
			if (_hitbox != null)
				_hitbox.Monitoring = true;
			_animatedSprite.Play("idle");
			return;
		}

		if (_action == BossAction.Boulder && _animatedSprite.Animation == "attack_boulder")
		{
			ReturnToIdle();
			return;
		}

		if (_animatedSprite.Animation == "hit")
		{
			_isHit = false;
			_action = BossAction.Idle;
			_animatedSprite.SelfModulate = Colors.White;
			_animatedSprite.Play("idle");
		}
	}

	private void StartNextAttack()
	{
		FaceDirection(_player.GlobalPosition.X - GlobalPosition.X);
		Velocity = Vector2.Zero;
		if (_nextAttackIsBoulder)
		{
			_action = BossAction.Boulder;
			_boulderLaunched = false;
			_attackTimer = BoulderCooldown;
			_animatedSprite.Play("attack_boulder");
		}
		else
		{
			_action = BossAction.RockBarrage;
			_actionTimer = RockBarrageDuration;
			_lastAttackFrame = -1;
			_attackTimer = RockBarrageCooldown;
			_animatedSprite.Play("attack_barrage");
		}
		_nextAttackIsBoulder = !_nextAttackIsBoulder;
	}

	private void ReturnToIdle()
	{
		_action = BossAction.Idle;
		_lastAttackFrame = -1;
		_animatedSprite.Play("idle");
	}

	private void ThrowSmallRock(int side)
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player))
			return;
		Vector2 origin = GlobalPosition + new Vector2(side * 24.0f, -58.0f);
		Vector2 direction = origin.DirectionTo(_player.GlobalPosition);
		LaunchRock(origin, direction, SmallRockDamage, SmallRockSpeed, false,
			Mathf.Clamp(origin.DistanceTo(_player.GlobalPosition) / SmallRockSpeed, 0.45f, 2.4f));
	}

	private void ThrowBoulder()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player))
			return;
		Vector2 origin = GlobalPosition + new Vector2(FaceDirectionSign() * 12.0f, -68.0f);
		Vector2 direction = origin.DirectionTo(_player.GlobalPosition);
		LaunchRock(origin, direction, BoulderDamage, BoulderSpeed, true,
			Mathf.Clamp(origin.DistanceTo(_player.GlobalPosition) / BoulderSpeed, 0.55f, 2.6f));
	}

	private float FaceDirectionSign() => _animatedSprite.FlipH ? -1.0f : 1.0f;

	private void LaunchRock(Vector2 origin, Vector2 direction, float damage, float speed, bool boulder, float flightTime)
	{
		PackedScene scene = RockScene ?? GD.Load<PackedScene>("res://Scenes/Effects/GolemRock.tscn");
		if (scene == null)
		{
			GD.PushError("Golem rock scene could not be loaded.");
			return;
		}

		GolemRock rock = scene.Instantiate<GolemRock>();
		rock.Setup(direction, damage, speed, flightTime, boulder, this);
		GetTree().CurrentScene.AddChild(rock);
		rock.GlobalPosition = origin;
	}
}
