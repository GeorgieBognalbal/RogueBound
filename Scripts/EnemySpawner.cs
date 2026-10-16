
using Godot;

public partial class EnemySpawner : Node2D
{
	[Export] public PackedScene EnemyScene;
	[Export] public NavigationRegion2D NavigationRegion;

	[Export] public float SpawnInterval = 3.0f;
	[Export] public int MaxEnemies = 20;
	[Export] public float MinDistanceFromPlayer = 250.0f;

	private Node2D _player;
	private float _timer;

	public override async void _Ready()
	{
		// add lagi sa group if may new enemy
		AddToGroup("enemies");

		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		// Give Godot time to synchronize the navigation map
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		if (_player == null)
			GD.PushError("EnemySpawner: No node found in the 'player' group.");

		if (EnemyScene == null)
			GD.PushError("EnemySpawner: Assign EnemyScene in the Inspector.");

		if (NavigationRegion == null)
			GD.PushError("EnemySpawner: Assign NavigationRegion in the Inspector.");
	}

	public override void _Process(double delta)
	{
		if (_player == null || EnemyScene == null || NavigationRegion == null)
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

			Node enemy = EnemyScene.Instantiate();

			// Add the enemy to the main scene, not the spawner
			GetTree().CurrentScene.AddChild(enemy);

			if (enemy is Node2D enemy2D)
				enemy2D.GlobalPosition = spawnPosition;

			enemy.AddToGroup("enemies");

			GD.Print("Enemy spawned at: ", spawnPosition);
			return;
		}

		GD.PushWarning(
            "EnemySpawner: Could not find a valid spawn position."
		);
	}
}
