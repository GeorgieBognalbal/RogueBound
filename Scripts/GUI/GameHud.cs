using Godot;

public partial class GameHud : Control
{
	private Label _coinLabel;
	private Label _waveLabel;
	private Label _enemiesLabel;

	private Player _player;
	private WaveManager _waveManager;

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

		_waveManager = GetTree().CurrentScene?.GetNodeOrNull<WaveManager>("WaveManager");
		if (_waveManager != null)
		{
			_waveManager.WaveChanged += OnWaveChanged;
			_waveManager.EnemiesCountChanged += OnEnemiesCountChanged;
			OnWaveChanged(_waveManager.CurrentWave, _waveManager.MaxWaves);
		}
	}

	public override void _Process(double delta)
	{
		// Fallback polling if WaveManager reference wasn't ready at _Ready
		if (_waveManager == null)
		{
			_waveManager = GetTree().CurrentScene?.GetNodeOrNull<WaveManager>("WaveManager");
			if (_waveManager != null)
			{
				_waveManager.WaveChanged += OnWaveChanged;
				_waveManager.EnemiesCountChanged += OnEnemiesCountChanged;
				OnWaveChanged(_waveManager.CurrentWave, _waveManager.MaxWaves);
			}
		}

		if (_player == null)
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Player;
			if (_player != null)
			{
				_player.MoneyChanged += OnMoneyChanged;
				OnMoneyChanged(_player.Money);
			}
		}
	}

	private void OnMoneyChanged(int newAmount)
	{
		if (_coinLabel != null)
			_coinLabel.Text = $"{newAmount}";
	}

	private void OnWaveChanged(int currentWave, int maxWaves)
	{
		if (_waveLabel != null)
		{
			_waveLabel.Text = $"WAVE {currentWave} / {maxWaves}";
		}
	}

	private void OnEnemiesCountChanged(int enemiesRemaining)
	{
		if (_enemiesLabel != null)
		{
			_enemiesLabel.Text = $"Enemies: {enemiesRemaining}";
		}
	}

	public override void _ExitTree()
	{
		if (_player != null)
			_player.MoneyChanged -= OnMoneyChanged;

		if (_waveManager != null)
		{
			_waveManager.WaveChanged -= OnWaveChanged;
			_waveManager.EnemiesCountChanged -= OnEnemiesCountChanged;
		}
	}
}
