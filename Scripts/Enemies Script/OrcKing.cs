using Godot;
using System.Collections.Generic;

public partial class OrcKing : OrcEnemy, IBossEncounter
{
	private enum ActionState { Entrance, Idle, Slash, Smash, Summon, Throw, Recovery, Dying }
	private readonly RandomNumberGenerator _rng = new();
	private readonly List<OrcEnemy> _summons = new();
	private ActionState _action = ActionState.Entrance;
	private CollisionShape2D _bodyShape;
	private Area2D _damageArea;
	private Player _lockedPlayer;
	private GameHud _bossHud;
	private bool _entranceStarted, _fightStarted, _roomActive = true;
	private bool _hitDone, _summoned, _axeThrown;
	private int _lastAttack = -1, _sameAttackCount;
	private float _cooldown, _recovery, _smashTelegraph;
	private float _summonCooldown = BossRoster.ORC_KING_SUMMON_COOLDOWN;
	private float _attackDamageMultiplier = 1.0f;
	private Vector2 _smashCenter;
	private OrcKingAxe _axe;

	public BossConfig BossConfig => BossRoster.ORC_KING;
	public bool IsFightStarted => _fightStarted;

	public override void _Ready()
	{
		float configuredHealth = MaxHealth;
		float configuredDamage = AttackDamage;
		ProcessMode = ProcessModeEnum.Pausable;
		base._Ready();
		MaxHealth = Mathf.Max(configuredHealth, BossRoster.ORC_KING_MAX_HP);
		_health = MaxHealth;
		Speed = BossRoster.ORC_KING_MOVE_SPEED;
		_attackDamageMultiplier = Mathf.Max(1.0f, configuredDamage / BossRoster.ORC_KING_SLASH_DAMAGE);
		AttackDamage = BossRoster.ORC_KING_SLASH_DAMAGE * _attackDamageMultiplier;
		StopDistance = 58.0f;
		_animatedSprite.SpriteFrames = BossSpriteFramesBuilder.FromConfig(BossConfig);
		_animatedSprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		_animatedSprite.Scale = Vector2.One;
		_animatedSprite.Offset = new Vector2(0, -5);
		_bodyShape = GetNodeOrNull<CollisionShape2D>("EnemyCollisionShape");
		if (_bodyShape != null) _bodyShape.Shape = new RectangleShape2D { Size = new Vector2(BossRoster.ORC_KING_BODY_WIDTH, BossRoster.ORC_KING_BODY_HEIGHT) };
		_damageArea = _hitbox;
		if (_damageArea != null) _damageArea.Monitoring = false;
		_rng.Randomize();
		_animatedSprite.Stop();
		Visible = false;
	}

	public override void _Draw()
	{
		if (_smashTelegraph <= 0) return;
		Vector2 local = _smashCenter - GlobalPosition;
		var rect = new Rect2(local - new Vector2(BossRoster.ORC_KING_SMASH_TELEGRAPH_WIDTH, BossRoster.ORC_KING_SMASH_TELEGRAPH_HEIGHT) * 0.5f,
			new Vector2(BossRoster.ORC_KING_SMASH_TELEGRAPH_WIDTH, BossRoster.ORC_KING_SMASH_TELEGRAPH_HEIGHT));
		DrawRect(rect, new Color(1.0f, 0.08f, 0.06f, 0.30f), true);
		DrawRect(rect, new Color(1.0f, 0.25f, 0.12f, 0.9f), false, 2.0f);
	}

	public override void _Process(double delta)
	{
		if (_action == ActionState.Slash && !_hitDone && _animatedSprite.Frame >= BossRoster.ORC_KING_SLASH_HIT_FRAME)
		{
			_hitDone = true;
			TrySlashHit();
		}
		if (_action == ActionState.Smash && _smashTelegraph > 0 && _animatedSprite.Frame >= BossRoster.ORC_KING_SMASH_HIT_FRAME)
		{
			_smashTelegraph = 0;
			ResolveSmash();
		}
		if (_action == ActionState.Smash && _smashTelegraph > 0)
		{
			_smashTelegraph -= (float)delta;
			QueueRedraw();
			if (_smashTelegraph <= 0) ResolveSmash();
		}
		if (_action == ActionState.Summon && !_summoned && _animatedSprite.Frame >= BossRoster.ORC_KING_SUMMON_START_FRAME)
		{
			_summoned = true;
			SummonOrcs();
		}
		if (_action == ActionState.Throw && !_axeThrown && _animatedSprite.Frame >= BossRoster.ORC_KING_AXE_RELEASE_FRAME)
		{
			_axeThrown = true;
			ThrowAxe();
		}
		if (_action == ActionState.Recovery)
		{
			if (_axe == null || !GodotObject.IsInstanceValid(_axe))
			{
				_recovery -= (float)delta;
				if (_recovery <= 0) { _action = ActionState.Idle; _animatedSprite.Play("idle"); }
			}
		}
		_summons.RemoveAll(e => !GodotObject.IsInstanceValid(e) || e.IsDead());
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_roomActive || !_fightStarted || _action is ActionState.Entrance or ActionState.Dying)
		{ Velocity = Vector2.Zero; MoveAndSlide(); return; }
		_cooldown = Mathf.Max(0, _cooldown - (float)delta);
		_summonCooldown = Mathf.Max(0, _summonCooldown - (float)delta);
		if (_action != ActionState.Idle) { Velocity = Vector2.Zero; MoveAndSlide(); return; }
		if (_player == null || !GodotObject.IsInstanceValid(_player)) { _player = GetTree().GetFirstNodeInGroup("player") as Node2D; return; }
		float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);
		if (_cooldown <= 0) { ChooseAttack(distance); return; }
		if (distance > BossRoster.ORC_KING_SLASH_RANGE * 0.8f)
		{
			Vector2 dir = GlobalPosition.DirectionTo(_player.GlobalPosition);
			Velocity = dir * Speed * (IsPhaseTwo() ? BossRoster.ORC_KING_PHASE_TWO_SPEED_MULTIPLIER : 1.0f);
			MoveAndSlide();
			FaceDirection(dir.X);
			if (_animatedSprite.Animation != "idle") _animatedSprite.Play("idle");
		}
		else { Velocity = Vector2.Zero; MoveAndSlide(); FaceDirection(_player.GlobalPosition.X - GlobalPosition.X); _animatedSprite.Play("idle"); }
	}

	protected override void TryAttackPlayer() { }

	private bool IsPhaseTwo() => _health / Mathf.Max(1.0f, MaxHealth) <= BossRoster.ORC_KING_PHASE_TWO_THRESHOLD;

	private void ChooseAttack(float distance)
	{
		int pick;
		bool canSummon = _summons.Count < BossRoster.ORC_KING_SUMMON_CAP;
		if (canSummon && (_summonCooldown <= 0 || _health / MaxHealth < 0.75f)) pick = 2;
		else if (distance > BossRoster.ORC_KING_THROW_RANGE) pick = 3;
		else pick = _rng.Randf() < 0.55f ? 0 : 1;
		if (_sameAttackCount >= 2 && pick == _lastAttack) pick = (pick + 1) % 4;
		if (pick == _lastAttack) _sameAttackCount++; else { _lastAttack = pick; _sameAttackCount = 1; }
		float mult = IsPhaseTwo() ? BossRoster.ORC_KING_PHASE_TWO_COOLDOWN_MULTIPLIER : 1.0f;
		_cooldown = (pick switch { 0 => BossRoster.ORC_KING_ATTACK_COOLDOWN, 1 => BossRoster.ORC_KING_SMASH_COOLDOWN, 2 => BossRoster.ORC_KING_SUMMON_COOLDOWN, _ => BossRoster.ORC_KING_THROW_COOLDOWN }) * mult;
		Velocity = Vector2.Zero;
		FaceDirection(_player.GlobalPosition.X - GlobalPosition.X);
		_hitDone = _summoned = _axeThrown = false;
		switch (pick)
		{
			case 0: _action = ActionState.Slash; _animatedSprite.Play("slash"); break;
			case 1:
				_action = ActionState.Smash;
				_smashCenter = _player.GlobalPosition; // locked when the charge starts
				_smashTelegraph = BossRoster.ORC_KING_SMASH_TELEGRAPH_SECONDS;
				_animatedSprite.Play("smash"); QueueRedraw(); break;
			case 2: _action = ActionState.Summon; _summonCooldown = BossRoster.ORC_KING_SUMMON_COOLDOWN; _animatedSprite.Play("summon"); break;
			default: _action = ActionState.Throw; _animatedSprite.Play("throw"); break;
		}
	}

	private void TrySlashHit()
	{
		if (_player is not Player target || !GodotObject.IsInstanceValid(target)) return;
		Vector2 toPlayer = target.GlobalPosition - GlobalPosition;
		float forward = _animatedSprite.FlipH ? -1 : 1;
		if (toPlayer.Length() <= BossRoster.ORC_KING_SLASH_RANGE && Mathf.Abs(Mathf.RadToDeg(Mathf.Atan2(toPlayer.Y, toPlayer.X * forward))) <= BossRoster.ORC_KING_SLASH_ARC_DEGREES * 0.5f)
			target.TakeDamage(BossRoster.ORC_KING_SLASH_DAMAGE * _attackDamageMultiplier, this);
	}

	private void ResolveSmash()
	{
		QueueRedraw();
		if (_player is not Player target || !GodotObject.IsInstanceValid(target)) return;
		var rect = new Rect2(_smashCenter - new Vector2(BossRoster.ORC_KING_SMASH_TELEGRAPH_WIDTH, BossRoster.ORC_KING_SMASH_TELEGRAPH_HEIGHT) * 0.5f,
			new Vector2(BossRoster.ORC_KING_SMASH_TELEGRAPH_WIDTH, BossRoster.ORC_KING_SMASH_TELEGRAPH_HEIGHT));
		if (rect.HasPoint(target.GlobalPosition))
		{
			target.TakeDamage(BossRoster.ORC_KING_SMASH_DAMAGE * _attackDamageMultiplier, this);
			target.TryApplyStun(BossRoster.ORC_KING_SMASH_STUN_SECONDS);
		}
	}

	private void SummonOrcs()
	{
		var scene = GD.Load<PackedScene>("res://Scenes/Enemies/OrcEnemy.tscn");
		var map = GetTree().CurrentScene?.GetNodeOrNull<TileMapLayer>("TileMap/Layer1") ?? GetTree().CurrentScene?.GetNodeOrNull<TileMapLayer>("Layer1");
		if (scene == null || map == null) return;
		for (int i = 0; i < BossRoster.ORC_KING_SUMMON_COUNT && _summons.Count < BossRoster.ORC_KING_SUMMON_CAP; i++)
		{
			for (int attempt = 0; attempt < 30; attempt++)
			{
				Vector2 candidate = GlobalPosition + new Vector2(_rng.RandfRange(-150, 150), _rng.RandfRange(-115, 115));
				Vector2I cell = map.LocalToMap(map.ToLocal(candidate));
				if (map.GetCellSourceId(cell) < 0 || map.GetCellAtlasCoords(cell).X >= 11) continue;
				var orc = scene.Instantiate<OrcEnemy>();
				GetTree().CurrentScene.AddChild(orc);
				orc.GlobalPosition = map.ToGlobal(map.MapToLocal(cell));
				_summons.Add(orc);
				break;
			}
		}
	}

	private void ThrowAxe()
	{
		var axe = new OrcKingAxe();
		axe.Setup(_player?.GlobalPosition ?? GlobalPosition + Vector2.Right, this, _attackDamageMultiplier);
		axe.GlobalPosition = GlobalPosition + new Vector2(_animatedSprite.FlipH ? -30 : 30, -18);
		GetTree().CurrentScene.AddChild(axe);
		_axe = axe;
	}

	public void AxeReturned(OrcKingAxe axe) { if (_axe == axe) _axe = null; }

	public override void TakeDamage(float damage, Vector2 attackerPosition)
	{
		if (!_fightStarted || _action == ActionState.Dying || _isDead) return;
		_health -= damage;
		GetNodeOrNull<AudioStreamPlayer2D>("HitBoxSound")?.Play();
		if (_player is Player player) player.ShakeCamera(0.85f);
		SpawnDamageNumber(damage);
		if (_health <= 0)
		{
			_isDead = true; _fightStarted = false; _action = ActionState.Dying; Velocity = Vector2.Zero;
			CollisionLayer = 0;
			if (_bodyShape != null) _bodyShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
			if (_damageArea != null) _damageArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
			GetNodeOrNull<AudioStreamPlayer2D>("EnemyKill")?.Play();
			GetNodeOrNull<DatabaseManager>("/root/DatabaseManager")?.RecordEnemyKill("OrcKing");
			SpawnDrops();
			GetBossHud()?.HideBossBar(this);
			CleanupBossEffects();
			_animatedSprite.Play("death");
		}
	}

	protected override async void OnAnimationFinished()
	{
		if (_action == ActionState.Dying)
		{
			_animatedSprite.Stop(); _animatedSprite.Frame = _animatedSprite.SpriteFrames.GetFrameCount("death") - 1;
			await ToSignal(GetTree().CreateTimer(BossRoster.ORC_KING_DEATH_HOLD_SECONDS), SceneTreeTimer.SignalName.Timeout);
			if (GodotObject.IsInstanceValid(this)) QueueFree();
			return;
		}
		if (_action == ActionState.Entrance) { StartFight(); return; }
		if (_action is ActionState.Slash or ActionState.Smash or ActionState.Summon or ActionState.Throw)
		{
			_action = ActionState.Recovery; _recovery = BossRoster.ORC_KING_RECOVERY_SECONDS;
			if (_axe != null && GodotObject.IsInstanceValid(_axe))
			{
				_animatedSprite.Frame = _animatedSprite.SpriteFrames.GetFrameCount("throw") - 1;
				_animatedSprite.Stop();
			}
			else _animatedSprite.Play("idle");
		}
	}

	public async void StartBossIntroOnce(Player player)
	{
		if (_entranceStarted) return;
		_entranceStarted = true; _lockedPlayer = player; player?.BeginBossIntroLock(); _bossHud = GetBossHud(); _bossHud?.HideBossBar(); _bossHud?.SetBossIntroPending(true);
		await ToSignal(GetTree().CreateTimer(BossRoster.ORC_KING_PREINTRO_DELAY_SECONDS), SceneTreeTimer.SignalName.Timeout);
		if (!GodotObject.IsInstanceValid(this) || _isDead) return;
		string folder = "res://Assets/Enemies/OrcKing/IntroBanner";
		var intro = OrcKingIntro.Play(this, folder);
		await ToSignal(intro, OrcKingIntro.SignalName.Finished);
		if (!GodotObject.IsInstanceValid(this) || _isDead) return;
		Visible = true; _action = ActionState.Entrance; _animatedSprite.Play("spawn");
	}

	private void StartFight()
	{
		_fightStarted = true; _action = ActionState.Idle; _animatedSprite.Play("idle");
		if (_damageArea != null) _damageArea.Monitoring = true;
		GetBossHud()?.ShowBossBar(this, BossConfig);
		if (_lockedPlayer != null && GodotObject.IsInstanceValid(_lockedPlayer)) _lockedPlayer.EndBossIntroLock();
	}

	public void ShowBossBarIfFightStarted() { if (_fightStarted && !_isDead) GetBossHud()?.ShowBossBar(this, BossConfig); }
	public void SetRoomActive(bool active) { _roomActive = active; if (!active) GetBossHud()?.HideBossBar(this); }
	public void HideBossBar() => GetBossHud()?.HideBossBar(this);
	public void CleanupBossEffects()
	{
		if (_axe != null && GodotObject.IsInstanceValid(_axe)) _axe.QueueFree();
		_axe = null;
		foreach (var summon in _summons) if (GodotObject.IsInstanceValid(summon)) summon.QueueFree();
		_summons.Clear();
	}

	private GameHud GetBossHud()
	{
		if (_bossHud != null && GodotObject.IsInstanceValid(_bossHud)) return _bossHud;
		foreach (Node n in GetTree().GetNodesInGroup("game_hud")) if (n is GameHud hud) return _bossHud = hud;
		return null;
	}
}
