using Godot;
using System;
using RogueBound.Dungeon;
using RogueBound.Weapons;

public partial class GameHud : Control
{
	private Label _coinLabel;
	private Label _waveLabel;
	private Label _enemiesLabel;
	private Label _potionEffectLabel;
	private VBoxContainer _potionStatus;
	private TextureRect _potionIcon;
	private ProgressBar _potionTimeBar;
	private ShopConsumable _displayedPotion;

	private TextureRect _slot0Icon;
	private TextureRect _slot1Icon;
	private Panel _slot0Frame;
	private Panel _slot1Frame;
	private ProgressBar _slot0DashCooldownBar;
	private ProgressBar _slot1DashCooldownBar;
	private WeaponComponent _weaponComponent;
	private Label _swapHintLabel;

	private Player _player;
	private WaveManager _waveManager;
	private DungeonManager _dungeonManager;
	private int _challengeNoticeVersion;
	private Control _bossUi;
	private Label _bossNameLabel;
	private Label _bossTitleLabel;
	private Panel _bossBarFrame;
	private ColorRect _bossBarTrail;
	private ColorRect _bossBarFill;
	private Enemy _shownBoss;
	private float _bossFillRatio;
	private float _bossTrailRatio;
	private float _bossTrailDelay;

	public override void _Ready()
	{
		AddToGroup("game_hud");
		_coinLabel = GetNodeOrNull<Label>("TopRight/HBoxContainer/CoinLabel");
		_waveLabel = GetNodeOrNull<Label>("TopCenter/VBoxContainer/WaveLabel");
		_enemiesLabel = GetNodeOrNull<Label>("TopCenter/VBoxContainer/EnemiesLabel");
		_potionStatus = GetNode<VBoxContainer>("HealthBar/PotionStatus");
		_potionIcon = _potionStatus.GetNode<TextureRect>("Effect/Icon");
		_potionEffectLabel = _potionStatus.GetNode<Label>("Effect/Countdown");
		_potionTimeBar = _potionStatus.GetNode<ProgressBar>("TimeBar");
		_potionTimeBar.MaxValue = Player.PotionDuration;

		_slot0Icon = GetNodeOrNull<TextureRect>("BottomCenter/WeaponSlots/Slot0/Icon");
		_slot1Icon = GetNodeOrNull<TextureRect>("BottomCenter/WeaponSlots/Slot1/Icon");
		_slot0Frame = GetNodeOrNull<Panel>("BottomCenter/WeaponSlots/Slot0");
		_slot1Frame = GetNodeOrNull<Panel>("BottomCenter/WeaponSlots/Slot1");

		_slot0DashCooldownBar = CreateDashCooldownBar(_slot0Frame, "Slot0DashCooldownBar");
		_slot1DashCooldownBar = CreateDashCooldownBar(_slot1Frame, "Slot1DashCooldownBar");
		_swapHintLabel = GetNodeOrNull<Label>("BottomCenter/SwapHint");

		_player = GetTree().GetFirstNodeInGroup("player") as Player;
		if (_player != null)
		{
			_player.MoneyChanged += OnMoneyChanged;
			OnMoneyChanged(_player.Money);
			HookPlayerWeapons();
		}

		CreateBossBar();
		HookManagers();
	}

	private void HookPlayerWeapons()
	{
		if (_player == null) return;
		_weaponComponent = _player.GetNodeOrNull<WeaponComponent>("WeaponComponent");
		if (_weaponComponent != null)
		{
			_weaponComponent.WeaponChanged += OnWeaponChanged;
			OnWeaponChanged(_weaponComponent.PrimaryWeapon, _weaponComponent.SecondaryWeapon, _weaponComponent.ActiveSlot);
		}
	}

	private void OnWeaponChanged(RogueBound.Weapons.WeaponData primary, RogueBound.Weapons.WeaponData secondary, int activeSlot)
	{
		if (_slot0Icon != null)
		{
			if (primary != null && !string.IsNullOrEmpty(primary.IconPath))
			{
				_slot0Icon.Texture = GD.Load<Texture2D>(primary.IconPath);
				_slot0Icon.Visible = true;
			}
			else
			{
				_slot0Icon.Texture = null;
				_slot0Icon.Visible = false;
			}
		}

		if (_slot1Icon != null)
		{
			if (secondary != null && !string.IsNullOrEmpty(secondary.IconPath))
			{
				_slot1Icon.Texture = GD.Load<Texture2D>(secondary.IconPath);
				_slot1Icon.Visible = true;
			}
			else
			{
				_slot1Icon.Texture = null;
				_slot1Icon.Visible = false;
			}
		}

		// Visual active slot highlight
		if (_slot0Frame != null)
		{
			_slot0Frame.Modulate = activeSlot == 0 ? Colors.White : new Color(0.5f, 0.5f, 0.5f, 0.6f);
		}
		if (_slot1Frame != null)
		{
			_slot1Frame.Modulate = activeSlot == 1 ? Colors.White : new Color(0.5f, 0.5f, 0.5f, 0.6f);
		}
		if (_swapHintLabel != null)
		{
			var activeWeapon = activeSlot == 0 ? primary : secondary;
			_swapHintLabel.Text = activeWeapon?.Id == "dagger"
				? "[Q] Swap  [G] Drop  ·  ATTACK TO DASH"
				: "[Q] Swap  [G] Drop";
		}
	}

	public override void _Process(double delta)
	{
		UpdateBossBar();
		if (_player != null)
		{
			float remaining = _player.ActivePotionTimeRemaining;
			var potion = _player.ActivePotion;
			_potionStatus.Visible = potion != ShopConsumable.None && _player.health > 0f;
			if (_potionStatus.Visible)
			{
				var info = ConsumableInfo.Get(potion);
				if (_displayedPotion != potion)
				{
					_displayedPotion = potion;
					_potionIcon.Texture = GD.Load<Texture2D>(info.IconPath);
					_potionIcon.Modulate = info.Tint;
				}
				_potionEffectLabel.Text = $"{info.Name}  {Mathf.CeilToInt(remaining)}s";
				_potionTimeBar.MaxValue = _player.ActivePotionDuration;
				_potionTimeBar.Value = remaining;
			}
		}

		if (_player == null)
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Player;
			if (_player != null)
			{
				_player.MoneyChanged += OnMoneyChanged;
				OnMoneyChanged(_player.Money);
				HookPlayerWeapons();
			}
		}

		UpdateDashCooldownBars();

		if (_dungeonManager == null && _waveManager == null)
		{
			HookManagers();
		}
	}

	private void CreateBossBar()
	{
		float width = GolemBossSettings.BOSS_BAR_WIDTH;
		float viewportWidth = GetViewportRect().Size.X;
		float contentHeight = GolemBossSettings.BOSS_BAR_TITLE_Y_OFFSET + GolemBossSettings.BOSS_TITLE_LABEL_HEIGHT + GolemBossSettings.BOSS_BAR_HEIGHT;
		_bossUi = new Control
		{
			Name = "BossStatus",
			AnchorLeft = 0.0f,
			AnchorRight = 0.0f,
			AnchorTop = 0.0f,
			AnchorBottom = 0.0f,
			Position = new Vector2(0, GolemBossSettings.BOSS_BAR_TOP + GolemBossSettings.BOSS_BAR_GROUP_ENTER_OFFSET),
			Size = new Vector2(viewportWidth, contentHeight),
			ZIndex = 100,
			Visible = false,
			Modulate = new Color(1, 1, 1, 0),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_bossNameLabel = new Label { Name = "BossName", Text = GolemBossSettings.BOSS_NAME, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
		_bossNameLabel.Position = new Vector2((viewportWidth - width) / 2, GolemBossSettings.BOSS_BAR_NAME_Y_OFFSET);
		_bossNameLabel.Size = new Vector2(width, GolemBossSettings.BOSS_NAME_LABEL_HEIGHT);
		_bossNameLabel.AddThemeFontSizeOverride("font_size", (int)GolemBossSettings.BOSS_NAME_FONT_SIZE);
		_bossNameLabel.AddThemeColorOverride("font_color", new Color(GolemBossSettings.BOSS_TEXT_COLOR));
		_bossNameLabel.AddThemeColorOverride("font_outline_color", new Color(GolemBossSettings.BOSS_TEXT_OUTLINE_COLOR));
		_bossNameLabel.AddThemeConstantOverride("outline_size", GolemBossSettings.BOSS_NAME_OUTLINE_SIZE);
		_bossTitleLabel = new Label { Name = "BossTitle", Text = GolemBossSettings.BOSS_TITLE, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
		_bossTitleLabel.Position = new Vector2((viewportWidth - width) / 2, GolemBossSettings.BOSS_BAR_TITLE_Y_OFFSET);
		_bossTitleLabel.Size = new Vector2(width, GolemBossSettings.BOSS_TITLE_LABEL_HEIGHT);
		_bossTitleLabel.AddThemeFontSizeOverride("font_size", (int)GolemBossSettings.BOSS_TITLE_FONT_SIZE);
		_bossTitleLabel.AddThemeColorOverride("font_color", new Color(GolemBossSettings.BOSS_TEXT_COLOR));
		_bossTitleLabel.AddThemeColorOverride("font_outline_color", new Color(GolemBossSettings.BOSS_TEXT_OUTLINE_COLOR));
		_bossTitleLabel.AddThemeConstantOverride("outline_size", GolemBossSettings.BOSS_TITLE_OUTLINE_SIZE);
		var frameStyle = new StyleBoxFlat
		{
			BgColor = new Color(GolemBossSettings.BOSS_BAR_TRACK_COLOR),
			BorderColor = new Color(GolemBossSettings.BOSS_BAR_BORDER_COLOR),
			BorderWidthLeft = (int)GolemBossSettings.BOSS_BAR_BORDER_SIZE,
			BorderWidthRight = (int)GolemBossSettings.BOSS_BAR_BORDER_SIZE,
			BorderWidthTop = (int)GolemBossSettings.BOSS_BAR_BORDER_SIZE,
			BorderWidthBottom = (int)GolemBossSettings.BOSS_BAR_BORDER_SIZE,
			CornerRadiusTopLeft = 0,
			CornerRadiusTopRight = 0,
			CornerRadiusBottomLeft = 0,
			CornerRadiusBottomRight = 0,
		};
		_bossBarFrame = new Panel { Name = "BossBarFrame", Position = new Vector2((viewportWidth - width) / 2, GolemBossSettings.BOSS_BAR_TITLE_Y_OFFSET + GolemBossSettings.BOSS_TITLE_LABEL_HEIGHT), Size = new Vector2(width, GolemBossSettings.BOSS_BAR_HEIGHT), MouseFilter = MouseFilterEnum.Ignore };
		_bossBarFrame.AddThemeStyleboxOverride("panel", frameStyle);
		float inset = GolemBossSettings.BOSS_BAR_BORDER_SIZE;
		float fillWidth = width - inset * 2;
		float fillHeight = GolemBossSettings.BOSS_BAR_HEIGHT - inset * 2;
		_bossBarTrail = new ColorRect { Name = "BossBarDamageTrail", Color = new Color(GolemBossSettings.BOSS_BAR_TRAIL_COLOR), Position = new Vector2(inset, inset), Size = new Vector2(fillWidth, fillHeight), MouseFilter = MouseFilterEnum.Ignore };
		_bossBarFill = new ColorRect { Name = "BossBarFill", Color = new Color(GolemBossSettings.BOSS_BAR_FILL_COLOR), Position = new Vector2(inset, inset), Size = new Vector2(fillWidth, fillHeight), MouseFilter = MouseFilterEnum.Ignore };
		_bossBarFrame.AddChild(_bossBarTrail);
		_bossBarFrame.AddChild(_bossBarFill);
		_bossUi.AddChild(_bossNameLabel);
		_bossUi.AddChild(_bossTitleLabel);
		_bossUi.AddChild(_bossBarFrame);
		AddChild(_bossUi);
	}

	public void ShowBossBar(Enemy boss, BossConfig config)
	{
		_shownBoss = boss;
		if (boss == null) { HideBossBar(); return; }
		_bossNameLabel.Text = config.Name;
		_bossTitleLabel.Text = config.Title;
		_bossTitleLabel.AddThemeFontSizeOverride("font_size", config.Title.Length > 20 ? 15 : (int)GolemBossSettings.BOSS_TITLE_FONT_SIZE);
		_bossUi.Visible = true;
		_bossFillRatio = _bossTrailRatio = GetBossHealthRatio();
		_bossTrailDelay = 0.0f;
		UpdateBossBar();
		_bossUi.Modulate = new Color(1, 1, 1, 0);
		_bossUi.Position = new Vector2(0, GolemBossSettings.BOSS_BAR_TOP + GolemBossSettings.BOSS_BAR_GROUP_ENTER_OFFSET);
		Tween tween = CreateTween().SetParallel(true);
		tween.TweenProperty(_bossUi, "modulate:a", 1.0f, GolemBossSettings.BOSS_BAR_ENTER_SECONDS);
		tween.TweenProperty(_bossUi, "position:y", GolemBossSettings.BOSS_BAR_TOP, GolemBossSettings.BOSS_BAR_ENTER_SECONDS).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	public void HideBossBar(Enemy boss = null)
	{
		if (boss != null && _shownBoss != boss) return;
		_shownBoss = null;
		if (_bossUi != null) _bossUi.Visible = false;
	}

	private void UpdateBossBar()
	{
		if (_shownBoss == null || !GodotObject.IsInstanceValid(_shownBoss)) { HideBossBar(); return; }
		float ratio = GetBossHealthRatio();
		if (ratio < _bossFillRatio)
			_bossTrailDelay = GolemBossSettings.BOSS_BAR_TRAIL_DELAY_SECONDS;
		else if (ratio > _bossTrailRatio)
			_bossTrailRatio = ratio;
		_bossFillRatio = ratio;
		float delta = (float)GetProcessDeltaTime();
		if (_bossTrailDelay > 0.0f)
			_bossTrailDelay = Mathf.Max(0.0f, _bossTrailDelay - delta);
		else
			_bossTrailRatio = Mathf.MoveToward(_bossTrailRatio, ratio, delta / GolemBossSettings.BOSS_BAR_TRAIL_DRAIN_SECONDS);
		float innerWidth = GolemBossSettings.BOSS_BAR_WIDTH - GolemBossSettings.BOSS_BAR_BORDER_SIZE * 2.0f;
		_bossBarFill.Size = new Vector2(Mathf.Floor(innerWidth * _bossFillRatio), _bossBarFill.Size.Y);
		_bossBarTrail.Size = new Vector2(Mathf.Floor(innerWidth * _bossTrailRatio), _bossBarTrail.Size.Y);
	}

	private float GetBossHealthRatio() => _shownBoss == null || !GodotObject.IsInstanceValid(_shownBoss)
		? 0.0f
		: Mathf.Clamp(_shownBoss.GetHealth() / Mathf.Max(1.0f, _shownBoss.MaxHealth), 0.0f, 1.0f);

	private ProgressBar CreateDashCooldownBar(Panel slot, string barName)
	{
		if (slot == null)
			return null;

		var bar = new ProgressBar
		{
			Name = barName,
			AnchorLeft = 0.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 0.0f,
			OffsetLeft = 1.0f,
			OffsetTop = -7.0f,
			OffsetRight = -1.0f,
			OffsetBottom = -3.0f,
			MinValue = 0.0f,
			MaxValue = 1.0f,
			ShowPercentage = false,
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		var background = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.04f, 0.14f, 0.95f),
			BorderColor = new Color(0.72f, 0.35f, 1.0f, 1.0f),
			BorderWidthTop = 1,
			BorderWidthBottom = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
		};
		var fill = new StyleBoxFlat
		{
			BgColor = new Color(0.72f, 0.35f, 1.0f, 1.0f),
		};
		bar.AddThemeStyleboxOverride("background", background);
		bar.AddThemeStyleboxOverride("fill", fill);
		slot.AddChild(bar);
		return bar;
	}

	private void UpdateDashCooldownBars()
	{
		if (_weaponComponent == null)
			return;

		UpdateDashCooldownBar(_slot0DashCooldownBar, _weaponComponent.PrimaryWeapon);
		UpdateDashCooldownBar(_slot1DashCooldownBar, _weaponComponent.SecondaryWeapon);
	}

	private void UpdateDashCooldownBar(ProgressBar bar, WeaponData weapon)
	{
		if (bar == null)
			return;

		float cooldown = weapon?.AttackDashCooldown ?? 0.0f;
		float remaining = _weaponComponent.DashCooldownRemaining;
		bool hasDashCooldown = weapon != null && weapon.AttackDashDistance > 0.0f && cooldown > 0.0f;
		bool isCoolingDown = hasDashCooldown && remaining > 0.0f;

		bar.Visible = isCoolingDown;
		bar.Value = isCoolingDown ? Mathf.Clamp(remaining / cooldown, 0.0f, 1.0f) : 0.0f;
	}
	private void HookManagers()
	{
		_dungeonManager = GetTree().CurrentScene?.GetNodeOrNull<DungeonManager>("DungeonManager");
		if (_dungeonManager != null)
		{
			_dungeonManager.ProgressChanged += OnDungeonProgressChanged;
			_dungeonManager.EnemiesCountChanged += OnDungeonEnemiesCountChanged;
			_dungeonManager.ChallengeStatusChanged += OnChallengeStatusChanged;
			_dungeonManager.ChallengeNotice += OnChallengeNotice;
			OnDungeonProgressChanged(_dungeonManager.ClearedRooms, _dungeonManager.TotalCombatRooms);
			return;
		}

		_waveManager = GetTree().CurrentScene?.GetNodeOrNull<WaveManager>("WaveManager");
		if (_waveManager != null)
		{
			_waveManager.WaveChanged += OnWaveChanged;
			_waveManager.EnemiesCountChanged += OnEnemiesCountChanged;
			OnWaveChanged(_waveManager.CurrentWave, _waveManager.MaxWaves);
		}
	}

	private void OnMoneyChanged(int newAmount)
	{
		if (_coinLabel != null)
			_coinLabel.Text = $"{newAmount}";
	}

	private void OnDungeonProgressChanged(int clearedRooms, int totalCombatRooms)
	{
		if (_waveLabel != null && _dungeonManager != null)
		{
			_waveLabel.Modulate = Colors.White;
			_waveLabel.Text = $"FLOOR {_dungeonManager.CurrentFloor}  ROOMS {clearedRooms}/{totalCombatRooms}";
		}
	}

	private void OnChallengeStatusChanged(string title, string hint)
	{
		_challengeNoticeVersion++;
		if (_waveLabel != null)
		{
			_waveLabel.Text = title;
			_waveLabel.Modulate = Colors.Gold;
		}
		if (_enemiesLabel != null)
			_enemiesLabel.Text = hint;
	}

	private async void OnChallengeNotice(string message)
	{
		if (_waveLabel == null)
			return;

		int version = ++_challengeNoticeVersion;
		_waveLabel.Text = message;
		_waveLabel.Modulate = message.StartsWith("FAILED", StringComparison.OrdinalIgnoreCase)
			? new Color(1.0f, 0.35f, 0.3f)
			: new Color(0.4f, 1.0f, 0.5f);
		await ToSignal(GetTree().CreateTimer(2.5f), SceneTreeTimer.SignalName.Timeout);

		if (version != _challengeNoticeVersion || !GodotObject.IsInstanceValid(_waveLabel))
			return;

		if (_dungeonManager != null)
			OnDungeonProgressChanged(_dungeonManager.ClearedRooms, _dungeonManager.TotalCombatRooms);
	}

	private void OnDungeonEnemiesCountChanged(int count)
	{
		if (_enemiesLabel != null)
		{
			_enemiesLabel.Text = count > 0 ? $"Enemies: {count}" : "Area Clear";
		}
	}

	private void OnWaveChanged(int currentWave, int maxWaves)
	{
		if (_waveLabel != null)
		{
			_waveLabel.Text = $"WAVE {currentWave} / {maxWaves}";
		}
	}

	private void OnEnemiesCountChanged(int count)
	{
		if (_enemiesLabel != null)
		{
			_enemiesLabel.Text = $"Enemies: {count}";
		}
	}

	public override void _ExitTree()
	{
		if (_player != null)
		{
			_player.MoneyChanged -= OnMoneyChanged;
			var wc = _player.GetNodeOrNull<WeaponComponent>("WeaponComponent");
			if (wc != null)
			{
				wc.WeaponChanged -= OnWeaponChanged;
			}
		}

		if (_dungeonManager != null)
		{
			_dungeonManager.ProgressChanged -= OnDungeonProgressChanged;
			_dungeonManager.EnemiesCountChanged -= OnDungeonEnemiesCountChanged;
			_dungeonManager.ChallengeStatusChanged -= OnChallengeStatusChanged;
			_dungeonManager.ChallengeNotice -= OnChallengeNotice;
		}

		if (_waveManager != null)
		{
			_waveManager.WaveChanged -= OnWaveChanged;
			_waveManager.EnemiesCountChanged -= OnEnemiesCountChanged;
		}
	}
}
