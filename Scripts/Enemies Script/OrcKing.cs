using Godot;

public partial class OrcKing : OrcEnemy, IBossEncounter
{
	private GameHud _bossHud;
	private Player _lockedPlayer;
	private bool _entranceStarted;
	private bool _fightStarted;
	private bool _roomActive = true;
	private bool _cleaned;

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
		AttackDamage = Mathf.Max(configuredDamage, BossRoster.ORC_KING_ATTACK_DAMAGE);
		AttackCooldown = BossRoster.ORC_KING_ATTACK_COOLDOWN;
		Speed = BossRoster.ORC_KING_MOVE_SPEED;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_roomActive || !_fightStarted)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}
		base._PhysicsProcess(delta);
	}

	public override void TakeDamage(float damage, Vector2 attackerPosition)
	{
		if (!_fightStarted || _isDead)
			return;
		base.TakeDamage(damage, attackerPosition);
		if (_isDead)
		{
			GetBossHud()?.HideBossBar(this);
			CleanupBossEffects();
		}
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
		if (_hitbox != null)
			_hitbox.Monitoring = false;
		await ToSignal(GetTree().CreateTimer(BossRoster.ORC_KING_PREINTRO_DELAY_SECONDS), SceneTreeTimer.SignalName.Timeout);
		if (!GodotObject.IsInstanceValid(this) || _isDead)
			return;
		Texture2D portrait = BossPortraitBuilder.FromPortrait(BossConfig.PortraitPath, BossConfig.FrameSize);
		Font font = GD.Load<Font>("res://Assets/Theme/m5x7.ttf");
		var intro = BossIntro.Play(this, portrait, BossConfig.Name.ToUpperInvariant(), BossConfig.Title, font,
			BossConfig.BannerColor, BossConfig.BannerEdgeColor);
		await ToSignal(intro, BossIntro.SignalName.Finished);
		if (!GodotObject.IsInstanceValid(this) || _isDead)
			return;
		_fightStarted = true;
		if (_hitbox != null)
			_hitbox.Monitoring = true;
		_bossHud?.ShowBossBar(this, BossConfig);
		if (_lockedPlayer != null && GodotObject.IsInstanceValid(_lockedPlayer))
			_lockedPlayer.EndBossIntroLock();
	}

	public void ShowBossBarIfFightStarted()
	{
		if (_fightStarted && !_isDead)
			GetBossHud()?.ShowBossBar(this, BossConfig);
	}

	public void SetRoomActive(bool active) => _roomActive = active;

	public void HideBossBar() => GetBossHud()?.HideBossBar(this);

	public void CleanupBossEffects()
	{
		if (_cleaned)
			return;
		_cleaned = true;
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
