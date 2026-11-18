using Godot;

public partial class GolemMiniboss : Enemy, IBossEncounter
{
	private enum BossAction { Idle, Spawning, RockBarrage, Boulder, Recovery, Dying }

	[Export] public PackedScene RockScene;
	[Export] public float AttackAnimationSpeed = 1.3f;
	[Export] public float CriticalWindow = 0.55f;
	[Export] public float CriticalMultiplier = 2.0f;
	[Export] public float AttackRange = GolemBossSettings.BOSS_ATTACK_RANGE;
	[Export] public float RockBarrageDuration = GolemBossSettings.BARRAGE_DURATION;
	[Export] public float RockBarrageCooldown = GolemBossSettings.BARRAGE_COOLDOWN;
	[Export] public float BoulderCooldown = GolemBossSettings.BOULDER_COOLDOWN;
	[Export] public float SmallRockDamage = GolemBossSettings.SMALL_ROCK_DAMAGE;
	[Export] public float BoulderDamage = GolemBossSettings.BOULDER_DAMAGE;
	[Export] public float SmallRockSpeed = GolemBossSettings.SMALL_ROCK_SPEED;
	[Export] public float BoulderSpeed = GolemBossSettings.BOULDER_SPEED;

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
	private GameHud _bossHud;
	private Player _lockedPlayer;
	private bool _entranceStarted;
	private bool _introFinished;
	private bool _spawnAnimationFinished;
	private bool _fightStarted;
	private bool _roomActive = true;
	public BossConfig BossConfig => BossRoster.GALIO;
	public bool IsFightStarted => _fightStarted;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Pausable;
		// A slow, durable enemy; floor scaling is applied by DungeonRoom before _Ready.
		Speed = GolemBossSettings.BOSS_MOVE_SPEED;
		StopDistance = GolemBossSettings.BOSS_STOP_DISTANCE;
		AttackRange = GolemBossSettings.BOSS_ATTACK_RANGE;
		AttackDamage = Mathf.Max(AttackDamage, GolemBossSettings.BOSS_BASE_CONTACT_DAMAGE);
		AttackCooldown = GolemBossSettings.BOSS_CONTACT_ATTACK_COOLDOWN;
		MaxHealth = Mathf.Max(MaxHealth, GolemBossSettings.BOSS_BASE_HEALTH);
		base._Ready();
		_arenaCenter = GlobalPosition;
		_criticalLabel = GetNode<Label>("CriticalCue");

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
		if (!_roomActive)
			return;
		if (_action == BossAction.RockBarrage)
		{
			int frame = _animatedSprite.Frame;
			if (frame != _lastAttackFrame)
			{
				_lastAttackFrame = frame;
				// Godot frame 0/2 are the sheet's first and third (left/right throw) frames.
				if (frame == GolemBossSettings.BARRAGE_LEFT_THROW_FRAME || frame == GolemBossSettings.BARRAGE_RIGHT_THROW_FRAME)
					ThrowSmallRock(frame == GolemBossSettings.BARRAGE_LEFT_THROW_FRAME ? -1 : 1);
			}
		}
		else if (_action == BossAction.Boulder && !_boulderLaunched && _animatedSprite.Frame == GolemBossSettings.BOULDER_THROW_FRAME)
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
		if (!_roomActive)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}
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
		if (!_fightStarted || _action == BossAction.Spawning || _action == BossAction.Dying || _isDead || damage <= 0)
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
			GetBossHud()?.HideBossBar(this);
			CleanupBossEffects();
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
			GetTree().CreateTimer(GolemBossSettings.BOSS_DEATH_HOLD_SECONDS).Timeout += QueueFree;
			return;
		}

		if (_action == BossAction.Spawning && _animatedSprite.Animation == "spawn")
		{
			_spawnAnimationFinished = true;
			_action = BossAction.Idle;
			_animatedSprite.Play("idle");
			TryStartFight();
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

	private void TryStartFight()
	{
		if (_fightStarted || !_introFinished || !_spawnAnimationFinished || _isDead) return;
		_fightStarted = true;
		_action = BossAction.Idle;
		if (_hitbox != null) _hitbox.Monitoring = true;
		_animatedSprite.Play("idle");
		GetBossHud()?.ShowBossBar(this, BossConfig);
		if (_lockedPlayer != null && GodotObject.IsInstanceValid(_lockedPlayer)) _lockedPlayer.EndBossIntroLock();
	}

	public async void StartBossIntroOnce(Player player)
	{
		if (_entranceStarted) return;
		_entranceStarted = true;
		_lockedPlayer = player;
		player?.BeginBossIntroLock();
		_bossHud = GetBossHud();
		_bossHud?.HideBossBar();
		_bossHud?.SetBossIntroPending(true);
		if (_animatedSprite.Animation.ToString() == "spawn")
			await ToSignal(_animatedSprite, AnimatedSprite2D.SignalName.AnimationFinished);
		if (!GodotObject.IsInstanceValid(this) || _isDead)
			return;
		Texture2D portrait = GD.Load<Texture2D>(BossConfig.PortraitPath);
		Font pixelFont = GD.Load<Font>("res://Assets/Theme/m5x7.ttf");
		var intro = BossIntro.Play(this, portrait, BossConfig.Name.ToUpperInvariant(), BossConfig.Title, pixelFont,
			BossConfig.BannerColor, BossConfig.BannerEdgeColor);
		await ToSignal(intro, BossIntro.SignalName.Finished);
		if (!GodotObject.IsInstanceValid(this) || _isDead) return;
		_introFinished = true;
		TryStartFight();
	}

	public void ShowBossBarIfFightStarted()
	{
		if (_fightStarted && !_isDead)
			GetBossHud()?.ShowBossBar(this, BossConfig);
	}

	public void SetRoomActive(bool active)
	{
		_roomActive = active;
		if (!active && _action != BossAction.Spawning && _action != BossAction.Dying)
			ReturnToIdle();
	}

	public void HideBossBar()
	{
		GetBossHud()?.HideBossBar(this);
	}

	public void CleanupBossEffects()
	{
		foreach (Node node in GetTree().GetNodesInGroup("golem_rocks"))
			if (node is GolemRock rock && GodotObject.IsInstanceValid(rock) && rock.BelongsTo(this))
				rock.QueueFree();
	}

	private GameHud GetBossHud()
	{
		if (_bossHud != null && GodotObject.IsInstanceValid(_bossHud)) return _bossHud;
		foreach (Node node in GetTree().GetNodesInGroup("game_hud"))
			if (node is GameHud hud) return _bossHud = hud;
		return null;
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
		Vector2 origin = GlobalPosition + new Vector2(side * GolemBossSettings.SMALL_ROCK_ORIGIN_SIDE_OFFSET, GolemBossSettings.SMALL_ROCK_ORIGIN_HEIGHT);
		Vector2 direction = origin.DirectionTo(_player.GlobalPosition);
		LaunchRock(origin, direction, SmallRockDamage, SmallRockSpeed, false,
			Mathf.Clamp(origin.DistanceTo(_player.GlobalPosition) / SmallRockSpeed, GolemBossSettings.SMALL_ROCK_MIN_FLIGHT_SECONDS, GolemBossSettings.SMALL_ROCK_MAX_FLIGHT_SECONDS));
	}

	private void ThrowBoulder()
	{
		if (_player == null || !GodotObject.IsInstanceValid(_player))
			return;
		Vector2 origin = GlobalPosition + new Vector2(FaceDirectionSign() * GolemBossSettings.BOULDER_ORIGIN_SIDE_OFFSET, GolemBossSettings.BOULDER_ORIGIN_HEIGHT);
		Vector2 direction = origin.DirectionTo(_player.GlobalPosition);
		LaunchRock(origin, direction, BoulderDamage, BoulderSpeed, true,
			Mathf.Clamp(origin.DistanceTo(_player.GlobalPosition) / BoulderSpeed, GolemBossSettings.BOULDER_MIN_FLIGHT_SECONDS, GolemBossSettings.BOULDER_MAX_FLIGHT_SECONDS));
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
		CombatEffects.Burst(this, Colors.SandyBrown, origin - GlobalPosition, direction, boulder ? 18 : 7);
		(_player as Player)?.ShakeCamera(boulder ? 1.8f : 0.65f);
	}
}
