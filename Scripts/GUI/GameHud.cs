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

	public override void _Ready()
	{
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
			_dungeonManager.ChallengeTimerUpdated += OnChallengeTimerUpdated;
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

	private void OnChallengeTimerUpdated(float remaining, bool gasActive)
	{
		if (_waveLabel == null)
			return;

		if (remaining > 0.0f)
		{
			_challengeNoticeVersion++;
			_waveLabel.Text = $"⚠️ CHALLENGE: FIND KEY! [ {Mathf.CeilToInt(remaining)}s ]";
			_waveLabel.Modulate = new Color(1.0f, 0.85f, 0.2f);
		}
		else if (gasActive)
		{
			_waveLabel.Text = "☠️ GAS SPREADING! FIND KEY! ☠️";
			_waveLabel.Modulate = new Color(0.2f, 1.0f, 0.3f);
		}
		else
		{
			if (_dungeonManager != null)
			{
				OnDungeonProgressChanged(_dungeonManager.ClearedRooms, _dungeonManager.TotalCombatRooms);
			}
		}
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
			_dungeonManager.ChallengeTimerUpdated -= OnChallengeTimerUpdated;
			_dungeonManager.ChallengeNotice -= OnChallengeNotice;
		}

		if (_waveManager != null)
		{
			_waveManager.WaveChanged -= OnWaveChanged;
			_waveManager.EnemiesCountChanged -= OnEnemiesCountChanged;
		}
	}
}
