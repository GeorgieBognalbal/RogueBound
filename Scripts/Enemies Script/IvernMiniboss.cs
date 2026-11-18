using Godot;
using System.Collections.Generic;

public partial class IvernMiniboss : Enemy, IBossEncounter
{
	private enum AttackKind { Vines, Poison }
	private enum Action { Spawning, Idle, Vines, Poison, Dying }

	private readonly HashSet<IvernProjectile> _effects = new();
	private CollisionShape2D _bodyShape;
	private Action _action = Action.Spawning;
	private Player _lockedPlayer;
	private GameHud _bossHud;
	private float _attackCooldown;
	private int _lastAttackKind = -1;
	private int _sameAttackCount;
	private int _poisonLoops;
	private int _lastPoisonFrame = -1;
	private int _poisonEffectCount;
	private bool _entranceStarted;
	private bool _introFinished;
	private bool _spawnFinished;
	private bool _fightStarted;
	private bool _roomActive = true;
	private bool _vinesFired;
	private float _flashTime;
	private readonly RandomNumberGenerator _rng = new();

	public BossConfig BossConfig => BossRoster.IVERN;
	public bool IsFightStarted => _fightStarted;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Pausable;
		MaxHealth = Mathf.Max(MaxHealth, BossRoster.IVERN_MAX_HP);
		_health = MaxHealth;
		Speed = BossRoster.IVERN_MOVE_SPEED;
		StopDistance = 100000.0f;
		AttackDamage = Mathf.Max(AttackDamage, (int)BossRoster.IVERN_CONTACT_DAMAGE);
		base._Ready();
		_animatedSprite.SpriteFrames = BossSpriteFramesBuilder.FromConfig(BossConfig);
		_animatedSprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		_animatedSprite.Offset = new Vector2(0, BossRoster.IVERN_SPRITE_OFFSET_Y);
		_animatedSprite.Scale = Vector2.One * BossRoster.IVERN_VISUAL_SCALE;
		_bodyShape = GetNodeOrNull<CollisionShape2D>("EnemyCollisionShape");
		if (_bodyShape != null)
		{
			_bodyShape.Shape = new RectangleShape2D { Size = new Vector2(BossRoster.IVERN_BODY_WIDTH, BossRoster.IVERN_BODY_HEIGHT) };
			_bodyShape.Position = new Vector2(0.0f, BossRoster.IVERN_BODY_CENTER_Y);
		}
		if (_hitbox != null)
		{
			_hitbox.Monitoring = false;
			var damageShape = _hitbox.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			if (damageShape != null)
			{
				damageShape.Shape = new RectangleShape2D { Size = new Vector2(BossRoster.IVERN_DAMAGE_WIDTH, BossRoster.IVERN_DAMAGE_HEIGHT) };
				damageShape.Position = new Vector2(0.0f, BossRoster.IVERN_BODY_CENTER_Y);
			}
		}
		_rng.Randomize();
		_action = Action.Spawning;
		_animatedSprite.Play("spawn");
	}

	public override void _Process(double delta)
	{
		if (!_roomActive)
			return;
		if (_action == Action.Vines && !_vinesFired && _animatedSprite.Frame == BossRoster.VINE_BURST_FRAME)
		{
			_vinesFired = true;
			LaunchVineRing();
		}
		else if (_action == Action.Poison)
		{
			int frame = _animatedSprite.Frame;
			if (_lastPoisonFrame >= 0 && frame < _lastPoisonFrame)
			{
				_poisonLoops++;
				if (_poisonLoops >= BossRoster.POISON_BARRAGE_LOOPS)
				{
					ReturnToIdle();
					return;
				}
			}
			if (frame != _lastPoisonFrame)
			{
				_lastPoisonFrame = frame;
				if (frame == BossRoster.POISON_FIRST_SPIT_FRAME || frame == BossRoster.POISON_SECOND_SPIT_FRAME)
					SpitPoison();
			}
		}
		if (_flashTime > 0.0f)
		{
			_flashTime -= (float)delta;
			if (_flashTime <= 0.0f)
				_animatedSprite.SelfModulate = Colors.White;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_roomActive || !_fightStarted || _action == Action.Spawning || _action == Action.Dying || _isDead)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}
		_attackCooldown = Mathf.Max(0.0f, _attackCooldown - (float)delta);
		if (_action != Action.Idle)
			return;
		if (_player != null && GodotObject.IsInstanceValid(_player) && _attackCooldown <= 0.0f)
			StartRandomAttack();
	}

	protected override void TryAttackPlayer() { }

	public override void TakeDamage(float damage, Vector2 attackerPosition)
	{
		if (!_fightStarted || _action == Action.Spawning || _action == Action.Dying || _isDead)
			return;
		base.TakeDamage(damage, attackerPosition);
		if (_isDead)
		{
			_action = Action.Dying;
			CollisionLayer = 0;
			if (_bodyShape != null)
				_bodyShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
			if (_hitbox != null)
				_hitbox.SetDeferred(Area2D.PropertyName.Monitoring, false);
			_animatedSprite.SelfModulate = Colors.White;
			GetBossHud()?.HideBossBar(this);
			CleanupBossEffects();
		}
		else
		{
			_isHit = false;
			_hitTimer = 0.0f;
			_knockbackVelocity = Vector2.Zero;
			Velocity = Vector2.Zero;
			_animatedSprite.Play("idle");
			_animatedSprite.SelfModulate = new Color(1.0f, 0.68f, 0.68f, 1.0f);
			_flashTime = BossRoster.IVERN_HIT_FLASH_SECONDS;
		}
	}

	protected override async void OnAnimationFinished()
	{
		string animation = _animatedSprite.Animation.ToString();
		if (_action == Action.Spawning && animation == "spawn")
		{
			_spawnFinished = true;
			_action = Action.Idle;
			_animatedSprite.Play("idle");
			TryStartFight();
		}
		else if (_action == Action.Vines && animation == "attack_vines")
			ReturnToIdle();
		else if (_action == Action.Dying && animation == "death")
		{
			await ToSignal(GetTree().CreateTimer(GolemBossSettings.BOSS_DEATH_HOLD_SECONDS), SceneTreeTimer.SignalName.Timeout);
			if (GodotObject.IsInstanceValid(this))
				QueueFree();
		}
		else if (animation == "hit")
			_animatedSprite.Play("idle");
	}

	private void StartRandomAttack()
	{
		int pick = _rng.RandiRange(0, 1);
		if (_sameAttackCount >= BossRoster.IVERN_MAX_ATTACKS_IN_ROW && pick == _lastAttackKind)
			pick = 1 - pick;
		if (pick == _lastAttackKind)
			_sameAttackCount++;
		else
		{
			_lastAttackKind = pick;
			_sameAttackCount = 1;
		}
		_attackCooldown = BossRoster.IVERN_ATTACK_COOLDOWN;
		Velocity = Vector2.Zero;
		if (pick == (int)AttackKind.Vines)
		{
			_action = Action.Vines;
			_vinesFired = false;
			_animatedSprite.Play("attack_vines");
		}
		else
		{
			_action = Action.Poison;
			_poisonLoops = 0;
			_lastPoisonFrame = -1;
			_animatedSprite.Play("attack_poison");
		}
	}

	private void LaunchVineRing()
	{
		float startAngle = Mathf.DegToRad(_rng.RandfRange(0.0f, BossRoster.VINE_ROTATION_JITTER_DEGREES));
		for (int i = 0; i < BossRoster.VINE_COUNT; i++)
		{
			float angle = startAngle + Mathf.Tau * i / BossRoster.VINE_COUNT;
			Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
			var vine = new IvernProjectile();
			vine.SetupVine(direction, this);
			vine.GlobalPosition = GlobalPosition + new Vector2(0.0f, BossRoster.VINE_ORIGIN_Y);
			GetTree().CurrentScene.AddChild(vine);
			_effects.Add(vine);
		}
	}

	private void SpitPoison()
	{
		if (_poisonEffectCount >= BossRoster.POISON_MAX_PUDDLES || _player == null || !GodotObject.IsInstanceValid(_player))
			return;
		Vector2 origin = GlobalPosition + new Vector2(0.0f, BossRoster.POISON_ORIGIN_Y);
		float angle = _rng.RandfRange(0.0f, Mathf.Tau);
		float radius = _rng.RandfRange(0.0f, BossRoster.POISON_OFFSET_RADIUS);
		Vector2 target = _player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		var blob = new IvernProjectile();
		blob.SetupPoison(target, this);
		blob.GlobalPosition = origin;
		GetTree().CurrentScene.AddChild(blob);
		_effects.Add(blob);
		_poisonEffectCount++;
	}

	public void OnPoisonEffectFinished(IvernProjectile effect)
	{
		_effects.Remove(effect);
		_poisonEffectCount = Mathf.Max(0, _poisonEffectCount - 1);
	}

	private void ReturnToIdle()
	{
		_action = Action.Idle;
		_animatedSprite.Play("idle");
	}

	private void TryStartFight()
	{
		if (_fightStarted || !_introFinished || !_spawnFinished || _isDead)
			return;
		_fightStarted = true;
		_action = Action.Idle;
		if (_hitbox != null)
			_hitbox.Monitoring = true;
		_animatedSprite.Play("idle");
		GetBossHud()?.ShowBossBar(this, BossConfig);
		if (_lockedPlayer != null && GodotObject.IsInstanceValid(_lockedPlayer))
			_lockedPlayer.EndBossIntroLock();
	}

	public async void StartBossIntroOnce(Player player)
	{
		if (_entranceStarted)
			return;
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
		Texture2D portrait = BossPortraitBuilder.FromSheet(BossConfig);
		Font font = GD.Load<Font>("res://Assets/Theme/m5x7.ttf");
		var intro = BossIntro.Play(this, portrait, BossConfig.Name.ToUpperInvariant(), "THE GUARDIAN\nOF THE FOREST", font,
			BossConfig.BannerColor, BossConfig.BannerEdgeColor, portraitVerticalOffset: 0.08f, textHorizontalOffset: 0.05f);
		await ToSignal(intro, BossIntro.SignalName.Finished);
		if (!GodotObject.IsInstanceValid(this) || _isDead)
			return;
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
		if (!active && _action != Action.Spawning && _action != Action.Dying)
			ReturnToIdle();
	}

	public void HideBossBar() => GetBossHud()?.HideBossBar(this);

	public void CleanupBossEffects()
	{
		var effects = new IvernProjectile[_effects.Count];
		_effects.CopyTo(effects);
		foreach (IvernProjectile effect in effects)
			if (GodotObject.IsInstanceValid(effect))
				effect.RemoveImmediately();
		_effects.Clear();
		_poisonEffectCount = 0;
		(GetTree().GetFirstNodeInGroup("player") as Player)?.ClearRootEffect();
	}

	private GameHud GetBossHud()
	{
		if (_bossHud != null && GodotObject.IsInstanceValid(_bossHud))
			return _bossHud;
		foreach (Node node in GetTree().GetNodesInGroup("game_hud"))
			if (node is GameHud hud)
				return _bossHud = hud;
		return null;
	}
}
