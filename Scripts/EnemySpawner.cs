
using Godot;

public partial class EnemySpawner : Node2D
{
	[Export] public Godot.Collections.Array<PackedScene> EnemyScenes = new();
	[Export] public NavigationRegion2D NavigationRegion;

	[Export] public float SpawnInterval = 3.0f;
	[Export] public int MaxEnemies = 20;
	[Export] public float MinDistanceFromPlayer = 250.0f;

	private Node2D _player;
	private float _timer;
	private int _remainingToSpawn = 0;

	public int RemainingToSpawn => _remainingToSpawn;

	public override async void _Ready()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		// Wait for navigation to synchronize.
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		if (_player == null)
			GD.PushError("EnemySpawner: No player found in the 'player' group.");

		if (EnemyScenes == null || EnemyScenes.Count == 0)
			GD.PushError("EnemySpawner: Add at least one scene to EnemyScenes.");

		if (NavigationRegion == null)
			GD.PushError("EnemySpawner: Assign NavigationRegion in the Inspector.");
	}

	// Called by WaveManager when a new wave starts.
	public void StartWave(int amount)
	{
		_remainingToSpawn = Mathf.Max(amount, 0);
		_timer = SpawnInterval;

		GD.Print($"Spawner: Starting wave with {_remainingToSpawn} enemies.");
	}

	public override void _Process(double delta)
	{
		if (_remainingToSpawn <= 0)
			return;

		if (_player == null
			|| EnemyScenes == null
			|| EnemyScenes.Count == 0
			|| NavigationRegion == null)
			return;

		_timer += (float)delta;

		if (_timer >= SpawnInterval)
		{
			_timer = 0;

			// Decrease the remaining count only when spawning succeeds.
			if (TrySpawnEnemy())
				_remainingToSpawn--;

			if (_remainingToSpawn == 0)
				GD.Print("Spawner: All enemies for this wave have spawned.");
		}
	}

	private bool TrySpawnEnemy()
	{
		var enemies = GetTree().GetNodesInGroup("enemies");

		if (enemies.Count >= MaxEnemies)
			return false;

		var map = NavigationRegion.GetNavigationMap();

		for (int i = 0; i < 30; i++)
		{
			Vector2 spawnPosition =
				NavigationServer2D.MapGetRandomPoint(map, 1u, true);

			if (spawnPosition.DistanceTo(_player.GlobalPosition)
				< MinDistanceFromPlayer)
				continue;

			// Randomly choose an enemy scene.
			int randomIndex = GD.RandRange(0, EnemyScenes.Count - 1);
			PackedScene scene = EnemyScenes[randomIndex];

			if (scene == null)
				continue;

			Node enemy = scene.Instantiate();

			// Add enemy to the main gameplay scene.
			GetTree().CurrentScene.AddChild(enemy);

			if (enemy is Node2D enemy2D)
				enemy2D.GlobalPosition = spawnPosition;

			GD.Print($"{enemy.Name} spawned at: {spawnPosition}");

			return true;
		}

		GD.PushWarning("EnemySpawner: Could not find a valid spawn position.");
		return false;
	}
}
