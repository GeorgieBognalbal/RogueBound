using Godot;

public partial class EnemySpawner : Node2D
{
	// Add as many enemy scenes as you want in the Inspector.
	// Each spawn picks one at random. Add a scene twice to make it more common.
	[Export] public Godot.Collections.Array<PackedScene> EnemyScenes = new();
	[Export] public NavigationRegion2D NavigationRegion;

	[Export] public float SpawnInterval = 3.0f;
	[Export] public int MaxEnemies = 20;
	[Export] public float MinDistanceFromPlayer = 250.0f;

	private Node2D _player;
	private float _timer;

	public override async void _Ready()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		// Give Godot time to synchronize the navigation map
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		if (_player == null)
			GD.PushError("EnemySpawner: No node found in the 'player' group.");

		if (EnemyScenes == null || EnemyScenes.Count == 0)
			GD.PushError("EnemySpawner: Add at least one scene to EnemyScenes in the Inspector.");

		if (NavigationRegion == null)
			GD.PushError("EnemySpawner: Assign NavigationRegion in the Inspector.");
	}

	public override void _Process(double delta)
	{
		if (_player == null
			|| EnemyScenes == null
			|| EnemyScenes.Count == 0
			|| NavigationRegion == null)
			return;

		_timer += (float)delta;

		if (_timer >= SpawnInterval)
		{
			_timer = 0;
			TrySpawnEnemy();
		}
	}

	private void TrySpawnEnemy()
	{
		var enemies = GetTree().GetNodesInGroup("enemies");

		if (enemies.Count >= MaxEnemies)
			return;

		var map = NavigationRegion.GetNavigationMap();

		for (int i = 0; i < 30; i++)
		{
			Vector2 spawnPosition =
				NavigationServer2D.MapGetRandomPoint(map, 1u, true);

			if (spawnPosition.DistanceTo(_player.GlobalPosition)
				< MinDistanceFromPlayer)
				continue;

			// Pick a random enemy type
			PackedScene scene =
				EnemyScenes[GD.RandRange(0, EnemyScenes.Count - 1)];

			if (scene == null)
				continue;

			Node enemy = scene.Instantiate();

			// Add the enemy to the main scene, not the spawner
			GetTree().CurrentScene.AddChild(enemy);

			if (enemy is Node2D enemy2D)
				enemy2D.GlobalPosition = spawnPosition;

			// Enemy._Ready already adds itself to the "enemies" group.

			GD.Print($"{enemy.Name} spawned at: {spawnPosition}");
			return;
		}

		GD.PushWarning(
            "EnemySpawner: Could not find a valid spawn position."
		);
	}
}
