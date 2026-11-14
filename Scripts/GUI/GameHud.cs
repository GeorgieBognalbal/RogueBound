using Godot;
using RogueBound.Dungeon;

public partial class GameHud : Control
{
	private Label _coinLabel;
	private Label _waveLabel;
	private Label _enemiesLabel;
	private Label _potionEffectLabel;

	private TextureRect _slot0Icon;
	private TextureRect _slot1Icon;
	private Panel _slot0Frame;
	private Panel _slot1Frame;
	private Label _swapHintLabel;

	private Player _player;
	private WaveManager _waveManager;
	private DungeonManager _dungeonManager;

	public override void _Ready()
	{
		_coinLabel = GetNodeOrNull<Label>("TopRight/HBoxContainer/CoinLabel");
		_waveLabel = GetNodeOrNull<Label>("TopCenter/VBoxContainer/WaveLabel");
		_enemiesLabel = GetNodeOrNull<Label>("TopCenter/VBoxContainer/EnemiesLabel");
		var healthBar = GetNodeOrNull<Control>("HealthBar");
		if (healthBar != null)
		{
			_potionEffectLabel = new Label
			{
				Name = "PotionEffectLabel",
				Position = new Vector2(0.0f, 84.0f),
				Size = new Vector2(256.0f, 22.0f),
				Visible = false,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			_potionEffectLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
			_potionEffectLabel.AddThemeConstantOverride("outline_size", 3);
			_potionEffectLabel.AddThemeFontSizeOverride("font_size", 15);
			healthBar.AddChild(_potionEffectLabel);
		}

		_slot0Icon = GetNodeOrNull<TextureRect>("BottomCenter/WeaponSlots/Slot0/Icon");
		_slot1Icon = GetNodeOrNull<TextureRect>("BottomCenter/WeaponSlots/Slot1/Icon");
		_slot0Frame = GetNodeOrNull<Panel>("BottomCenter/WeaponSlots/Slot0");
		_slot1Frame = GetNodeOrNull<Panel>("BottomCenter/WeaponSlots/Slot1");
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
		var wc = _player.GetNodeOrNull<WeaponComponent>("WeaponComponent");
		if (wc != null)
		{
			wc.WeaponChanged += OnWeaponChanged;
			OnWeaponChanged(wc.PrimaryWeapon, wc.SecondaryWeapon, wc.ActiveSlot);
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
	}

	public override void _Process(double delta)
	{
		if (_player != null && _potionEffectLabel != null)
		{
			float remaining = _player.ActivePotionTimeRemaining;
			_potionEffectLabel.Visible = remaining > 0.0f;
			if (remaining > 0.0f)
				_potionEffectLabel.Text = $"{_player.ActivePotionName}: {Mathf.CeilToInt(remaining)}s";
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

		if (_dungeonManager == null && _waveManager == null)
		{
			HookManagers();
		}
	}

	private void HookManagers()
	{
		_dungeonManager = GetTree().CurrentScene?.GetNodeOrNull<DungeonManager>("DungeonManager");
		if (_dungeonManager != null)
		{
			_dungeonManager.ProgressChanged += OnDungeonProgressChanged;
			_dungeonManager.EnemiesCountChanged += OnDungeonEnemiesCountChanged;
			_dungeonManager.ChallengeTimerUpdated += OnChallengeTimerUpdated;
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
		}

		if (_waveManager != null)
		{
			_waveManager.WaveChanged -= OnWaveChanged;
			_waveManager.EnemiesCountChanged -= OnEnemiesCountChanged;
		}
	}
}
