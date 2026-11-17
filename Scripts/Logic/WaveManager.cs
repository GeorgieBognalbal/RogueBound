using Godot;
using System;

public partial class WaveManager : Node
{
	[Export] public PackedScene UpgradeScene;
	[Export] public PackedScene GameOverScene;

	[Export] public int EnemiesPerWave = 5;
	[Export] public float TimeBetweenWaves = 2.0f;
	[Export] public int MaxWaves = 10;

	public int CurrentWave => _currentWave;
	public event Action<int, int> WaveChanged;
	public event Action<int> EnemiesCountChanged;

	private int _currentWave = 0;
	private bool _waveActive = false;
	private bool _waitingForUpgrade = false;
	private bool _startingWave = false;
	private bool _isGameOver = false;

	private Player _player;

	public override void _Ready()
	{
		DatabaseManager database =
			GetNode<DatabaseManager>("/root/DatabaseManager");

		database.StartRun();

		_player = GetTree().GetFirstNodeInGroup("player") as Player;
		if (_player != null)
		{
			_player.Died += OnPlayerDied;
		}

		StartNextWave();
	}

	public override void _Process(double delta)
	{
		if (_isGameOver)
			return;

		if (_player == null)
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Player;
			if (_player != null)
				_player.Died += OnPlayerDied;
		}

		if (!_waveActive || _waitingForUpgrade)
			return;

		// Count living enemies.
		int aliveEnemies = 0;

		foreach (Node enemy in GetTree().GetNodesInGroup("enemies"))
		{
			if (enemy is Enemy e && !e.IsDead())
				aliveEnemies++;
		}

		EnemySpawner spawner =
			GetTree().CurrentScene.GetNodeOrNull<EnemySpawner>("EnemySpawner");

		int totalRemaining = aliveEnemies + (spawner != null ? spawner.RemainingToSpawn : 0);
		EnemiesCountChanged?.Invoke(totalRemaining);

		if (aliveEnemies == 0
			&& spawner != null
			&& spawner.RemainingToSpawn == 0)
		{
			EndWave();
		}
	}

	private async void StartNextWave()
	{
		if (_startingWave || _isGameOver)
			return;

		_startingWave = true;

		await ToSignal(
			GetTree().CreateTimer(TimeBetweenWaves),
			SceneTreeTimer.SignalName.Timeout
		);

		// Prevent starting if this manager has left the scene tree.
		if (!IsInsideTree() || _isGameOver)
			return;

		_currentWave++;
		_waveActive = true;

		GD.Print($"Wave {_currentWave} started!");
		WaveChanged?.Invoke(_currentWave, MaxWaves);

		EnemySpawner spawner =
			GetTree().CurrentScene.GetNodeOrNull<EnemySpawner>("EnemySpawner");

		if (spawner != null)
		{
			int spawnAmount = EnemiesPerWave + (_currentWave - 1) * 2;
			spawner.StartWave(spawnAmount);
			EnemiesCountChanged?.Invoke(spawnAmount);
		}
		else
		{
			GD.PushError("Could not find EnemySpawner!");
		}

		_startingWave = false;
	}

	private void EndWave()
	{
		_waveActive = false;

		GD.Print($"Wave {_currentWave} cleared!");

		// Check for victory condition!
		if (_currentWave >= MaxWaves)
		{
			TriggerVictory();
			return;
		}

		_waitingForUpgrade = true;
		ShowUpgradeMenu();
	}

	private void TriggerVictory()
	{
		if (_isGameOver)
			return;

		_isGameOver = true;
		GD.Print("RUN COMPLETE: VICTORY!");

		DatabaseManager database =
			GetNode<DatabaseManager>("/root/DatabaseManager");

		database.FinishRun("Victory");

		ShowGameOver(true);
	}

	private void OnPlayerDied()
	{
		if (_isGameOver)
			return;

		_isGameOver = true;
		_waveActive = false;
		GD.Print("PLAYER DIED - RUN ENDED!");

		DatabaseManager database =
			GetNode<DatabaseManager>("/root/DatabaseManager");

		database.FinishRun("Defeated");

		ShowGameOver(false);
	}

	private void ShowGameOver(bool isVictory)
	{
		GetTree().Paused = true;

		var scene = GameOverScene ?? GD.Load<PackedScene>("res://Scenes/Menus/GameOverMenu.tscn");
		if (scene != null)
		{
			var menu = scene.Instantiate<GameOverMenu>();
			GetTree().Root.AddChild(menu);

			int kills = 0;
			DatabaseManager db = GetNodeOrNull<DatabaseManager>("/root/DatabaseManager");
			if (db != null)
			{
				var summary = db.GetLastRunSummary();
				if (summary.ContainsKey("total_kills"))
					kills = (int)summary["total_kills"];
			}

			int coins = _player != null ? _player.Money : 0;
			menu.Setup(isVictory, _currentWave, MaxWaves, kills, coins);
		}
	}

	private void ShowUpgradeMenu()
	{
		if (UpgradeScene == null)
		{
			GD.PushError("Assign UpgradeScene in the WaveManager Inspector!");
			_waitingForUpgrade = false;
			StartNextWave();
			return;
		}

		CanvasLayer menu = UpgradeScene.Instantiate<CanvasLayer>();
		GetTree().Root.AddChild(menu);
		
		GetNodeOrNull<AudioStreamPlayer2D>("UpgradeSound")?.Play();
		menu.ProcessMode = Node.ProcessModeEnum.Always;

		// Find the inner Control that contains UpgradeScene.cs.
		Control upgradeControl = menu.GetNodeOrNull<Control>("UpgradeScene");

		if (upgradeControl != null)
		{
			upgradeControl.TreeExiting += OnUpgradeMenuClosed;
		}
		else
		{
			GD.PushError("Could not find the UpgradeScene Control inside CanvasLayer!");
		}

		GetTree().Paused = true;

		GD.Print("Upgrade menu opened!");
	}

	private void OnUpgradeMenuClosed()
	{
		GetTree().Paused = false;
		_waitingForUpgrade = false;
		StartNextWave();
	}
}
