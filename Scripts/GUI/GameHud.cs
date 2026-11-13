using Godot;
using RogueBound.Dungeon;

public partial class GameHud : Control
{
	private Label _coinLabel;
	private Label _waveLabel;
	private Label _enemiesLabel;

	private Player _player;
	private WaveManager _waveManager;
	private DungeonManager _dungeonManager;

	public override void _Ready()
	{
		_coinLabel = GetNodeOrNull<Label>("TopRight/HBoxContainer/CoinLabel");
		_waveLabel = GetNodeOrNull<Label>("TopCenter/VBoxContainer/WaveLabel");
		_enemiesLabel = GetNodeOrNull<Label>("TopCenter/VBoxContainer/EnemiesLabel");

		_player = GetTree().GetFirstNodeInGroup("player") as Player;
		if (_player != null)
		{
			_player.MoneyChanged += OnMoneyChanged;
			OnMoneyChanged(_player.Money);
		}

		HookManagers();
	}

	public override void _Process(double delta)
	{
		if (_player == null)
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Player;
			if (_player != null)
			{
				_player.MoneyChanged += OnMoneyChanged;
				OnMoneyChanged(_player.Money);
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
