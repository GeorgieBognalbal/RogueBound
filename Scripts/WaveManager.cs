
using Godot;

public partial class WaveManager : Node
{
	[Export] public PackedScene UpgradeScene;
	[Export] public int EnemiesPerWave = 5;
	[Export] public float TimeBetweenWaves = 2.0f;

	private int _currentWave = 0;
	private bool _waveActive = false;
	private bool _waitingForUpgrade = false;
	private bool _startingWave = false;

	public override void _Ready()
	{
		StartNextWave();
	}

	public override void _Process(double delta)
	{
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

		if (aliveEnemies == 0
			&& spawner != null
			&& spawner.RemainingToSpawn == 0)
		{
			EndWave();
		}
	}

	private async void StartNextWave()
	{
		if (_startingWave)
		{
			GD.Print("StartNextWave ignored: a wave is already starting.");
			return;
		}

		_startingWave = true;

		await ToSignal(
			GetTree().CreateTimer(TimeBetweenWaves),
			SceneTreeTimer.SignalName.Timeout
		);

		// Prevent starting if this manager has left the scene tree.
		if (!IsInsideTree())
			return;

		_currentWave++;
		_waveActive = true;

		GD.Print($"Wave {_currentWave} started!");

		EnemySpawner spawner =
			GetTree().CurrentScene.GetNodeOrNull<EnemySpawner>("EnemySpawner");

		if (spawner != null)
		{
			spawner.StartWave(
				EnemiesPerWave + (_currentWave - 1) * 2
			);
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
		_waitingForUpgrade = true;

		GD.Print($"Wave {_currentWave} cleared!");

		ShowUpgradeMenu();
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
