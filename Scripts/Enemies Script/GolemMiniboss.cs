using Godot;

public partial class GolemMiniboss : Enemy
{
	private enum BossAction { Idle, Spawning, RockBarrage, Boulder, Recovery, Dying }

	[Export] public PackedScene RockScene;
	[Export] public float AttackRange = 250.0f;
	[Export] public float RockBarrageDuration = 2.4f;
	[Export] public float RockBarrageCooldown = 3.6f;
	[Export] public float BoulderCooldown = 2.7f;
	[Export] public float SmallRockDamage = 12.0f;
	[Export] public float BoulderDamage = 24.0f;
	[Export] public float SmallRockSpeed = 130.0f;
	[Export] public float BoulderSpeed = 150.0f;
	[Export] public float AttackAnimationSpeed = 1.3f;
	[Export] public float CriticalWindow = 0.55f;
	[Export] public float CriticalMultiplier = 2.0f;

	private BossAction _action = BossAction.Spawning;
	private CollisionShape2D _bodyShape;
	private float _actionTimer;
	private int _lastAttackFrame = -1;
	private bool _boulderLaunched;
	private bool _nextAttackIsBoulder;
	private Vector2 _arenaCenter;
	private float _criticalRemaining;
	private float _flashRemaining;
	private Label _criticalLabel;
	private AudioStreamPlayer2D _throwSound;
	private AudioStreamPlayer2D _attackLandsSound;
	private AudioStreamPlayer2D _dieSound;

	public override void _Ready()
	{
		Speed = 0.0f;
		KnockbackForce = 0.0f;
		AttackDamage = Mathf.Max(AttackDamage, 18.0f);
		AttackCooldown = 2.0f;
		MaxHealth = Mathf.Max(MaxHealth, 420.0f);
		base._Ready();
		_arenaCenter = GlobalPosition;
		_criticalLabel = GetNode<Label>("CriticalCue");
		_throwSound = AddAudioPlayer("ThrowSound", "res://Sounds/Golem Throw.wav");
		_attackLandsSound = AddAudioPlayer("AttackLandsSound", "res://Sounds/Golem AttackLands.wav");
		_dieSound = AddAudioPlayer("DieSound", "res://Sounds/Golem Die Sound.wav");

		_bodyShape = GetNodeOrNull<CollisionShape2D>("EnemyCollisionShape");
		if (_hitbox != null)
			_hitbox.Monitoring = false;
		_action = BossAction.Spawning;
		_animatedSprite.Play("spawn");
		GolemShockwave.Spawn(this, 55f, CombatEffects.Impact);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_criticalRemaining = Mathf.Max(0, _criticalRemaining - dt);
		_flashRemaining = Mathf.Max(0, _flashRemaining - dt);
		_criticalLabel.Visible = _criticalRemaining > 0;
		_animatedSprite.SelfModulate = _flashRemaining > 0 ? Colors.White * 1.6f
			: _criticalRemaining > 0 ? Colors.Gold : Colors.White;
		QueueRedraw();
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

	public override void _Draw()
	{
		if (_criticalRemaining > 0)
		{
			DrawArc(Vector2.Zero, 28f, -Mathf.Pi / 2,
				-Mathf.Pi / 2 + Mathf.Tau * _criticalRemaining / Mathf.Max(CriticalWindow, 0.01f),
				32, Colors.Gold, 2f);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		Velocity = Vector2.Zero;
		GlobalPosition = _arenaCenter;
		if (_action == BossAction.Spawning || _action == BossAction.Dying || _isDead)
			return;

		_attackTimer = Mathf.Max(0.0f, _attackTimer - dt);

		if (_action == BossAction.RockBarrage)
		{
			_actionTimer -= dt;
			if (_actionTimer <= 0.0f)
				ReturnToIdle();
			return;
		}

		if (_action == BossAction.Boulder)
			return;

		if (_action == BossAction.Recovery)
		{
			_actionTimer -= dt;
			if (_actionTimer <= 0)
				_action = BossAction.Idle;
			return;
		}

		if (_player != null && GodotObject.IsInstanceValid(_player) && _attackTimer <= 0.0f &&
			GlobalPosition.DistanceTo(_player.GlobalPosition) <= AttackRange)
		{
			StartNextAttack();
			return;
		}
	}

	protected override void ApplyHitReaction(Vector2 attackerPosition) { }

	protected override void TryAttackPlayer()
	{
		// Attacks are started by the boss state machine above, never by contact damage.
	}

	public override void TakeDamage(float damage, Vector2 attackerPosition)
	{
		if (_action == BossAction.Spawning || _action == BossAction.Dying || _isDead || damage <= 0)
			return;

		if (_criticalRemaining > 0)
		{
			damage *= CriticalMultiplier;
			_criticalRemaining = 0;
			GolemShockwave.Spawn(this, 46f, Colors.Gold);
			CombatEffects.Burst(this, Colors.Gold, new Vector2(0, -24), amount: 22);
			(_player as Player)?.ShakeCamera(2.2f);
		}
		base.TakeDamage(damage, attackerPosition);
		if (_isDead)
		{
			_dieSound?.Play();
			_action = BossAction.Dying;
			_criticalRemaining = 0;
			_animatedSprite.SpeedScale = 1;
			GolemShockwave.Spawn(this, 85f, CombatEffects.Impact);
			CombatEffects.Burst(this, Colors.Gray, amount: 28);
			(_player as Player)?.ShakeCamera(2.8f);
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
			_flashRemaining = 0.08f;
		}
	}

	protected override void OnAnimationFinished()
	{
		if (_action == BossAction.Dying && _animatedSprite.Animation == "death")
		{
			// Hold the final rubble frame for a moment before cleaning up.
			GetTree().CreateTimer(3.0).Timeout += () =>
			{
				if (GodotObject.IsInstanceValid(this))
					QueueFree();
			};
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
		_animatedSprite.SpeedScale = Mathf.Max(0.1f, AttackAnimationSpeed);
		CombatEffects.Burst(this, Colors.SandyBrown, new Vector2(0, -28), amount: 12);
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
		_action = BossAction.Recovery;
		_actionTimer = Mathf.Max(0.85f, CriticalWindow);
		_criticalRemaining = Mathf.Max(0, CriticalWindow);
		_lastAttackFrame = -1;
		_animatedSprite.SpeedScale = 1;
		_animatedSprite.Play("idle");
		CombatEffects.Burst(this, Colors.Gold, new Vector2(0, -24));
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
		rock.Position = origin;
		GetTree().CurrentScene.AddChild(rock);
		_throwSound?.Play();
		CombatEffects.Burst(this, Colors.SandyBrown, origin - GlobalPosition, direction, boulder ? 18 : 7);
		(_player as Player)?.ShakeCamera(boulder ? 1.8f : 0.65f);
	}

	private AudioStreamPlayer2D AddAudioPlayer(string nodeName, string streamPath)
	{
		var player = new AudioStreamPlayer2D
		{
			Name = nodeName,
			Stream = GD.Load<AudioStream>(streamPath)
		};
		AddChild(player);
		return player;
	}

	public void PlayAttackLandsSound(Vector2 impactPosition)
	{
		if (_attackLandsSound == null || !GodotObject.IsInstanceValid(_attackLandsSound))
			return;
		_attackLandsSound.GlobalPosition = impactPosition;
		_attackLandsSound.Play();
	}
}
