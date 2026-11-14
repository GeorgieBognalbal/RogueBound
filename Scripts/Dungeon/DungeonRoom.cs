using Godot;
using System;
using System.Collections.Generic;

namespace RogueBound.Dungeon
{
	public partial class DungeonRoom : Node2D
	{
		public DungeonRoomData Data { get; set; }
		public List<DungeonDoor> Doors { get; } = new();
		public List<Enemy> ActiveEnemies { get; } = new();

		public event Action<DungeonRoom> PlayerEntered;
		public event Action<DungeonRoom> RoomCleared;
		public event Action<float, bool> ChallengeTimerUpdated;

		private Area2D _triggerArea;
		private CollisionShape2D _triggerCollision;
		private DungeonPortal _portal;
		private DungeonChest _challengeChest;
		private readonly List<DungeonChest> _roomClearLockedChests = new();
		private DungeonKey _dungeonKey;
		private PoisonGas _poisonGas;

		private bool _waveSpawned = false;
		private bool _challengeActive = false;
		private bool _challengeCompleted = false;
		private float _challengeTimer = 35.0f;
		private Player _playerEntering;
		private DungeonDoor _entryDoor;
		private bool _pendingDoorLock;

		public static readonly string[] EnemyScenePaths = new[]
		{
			"res://Scenes/Enemies/SkeletonEnemy.tscn",
			"res://Scenes/Enemies/ArcherEnemy.tscn",
			"res://Scenes/Enemies/SlimeEnemy.tscn",
			"res://Scenes/Enemies/ZombieEnemy.tscn",
			"res://Scenes/Enemies/OrcEnemy.tscn"
		};

		public override void _Ready()
		{
			_triggerArea = GetNodeOrNull<Area2D>("TriggerArea");
			if (_triggerArea != null)
			{
				_triggerArea.BodyEntered += OnTriggerBodyEntered;
			}
		}

		public void Initialize(DungeonRoomData data)
		{
			Data = data;
			GlobalPosition = data.WorldCenter;

			// Setup trigger collision shape to match room interior
			if (_triggerArea == null)
			{
				_triggerArea = new Area2D { Name = "TriggerArea" };
				_triggerArea.CollisionLayer = 0;
				_triggerArea.CollisionMask = 2; // Player layer
				AddChild(_triggerArea);
				_triggerArea.BodyEntered += OnTriggerBodyEntered;
			}

			_triggerCollision = new CollisionShape2D();
			var shape = new RectangleShape2D();
			shape.Size = new Vector2(
				Mathf.Max(data.WorldBounds.Size.X - 32.0f, 64.0f),
				Mathf.Max(data.WorldBounds.Size.Y - 32.0f, 64.0f)
			);
			_triggerCollision.Shape = shape;
			_triggerArea.AddChild(_triggerCollision);

			// Room-specific setup
			if (data.Type == RoomType.BossExit)
			{
				var portalScene = GD.Load<PackedScene>("res://Scenes/Dungeon/Portal.tscn");
				if (portalScene != null)
				{
					_portal = portalScene.Instantiate<DungeonPortal>();
					AddChild(_portal);
					_portal.Position = Vector2.Zero;
				}
			}
			else if (data.Type == RoomType.Challenge)
			{
				SetupChallengeRoom(data);
			}
			else if (data.Type == RoomType.Maze)
			{
				SetupMazeRoom(data);
			}
			else if (data.Type == RoomType.Shop)
			{
				SetupShopRoom(data);
			}
		}

		private void SetupChallengeRoom(DungeonRoomData data)
		{
			// 1. Poison Gas node covering room
			var gasScene = GD.Load<PackedScene>("res://Scenes/Dungeon/PoisonGas.tscn");
			if (gasScene != null)
			{
				_poisonGas = gasScene.Instantiate<PoisonGas>();
				AddChild(_poisonGas);
				_poisonGas.Position = Vector2.Zero;
				_poisonGas.SetupBounds(data.WorldBounds.Size);
			}

			// 2. Locked reward chest in center
			var chestScene = GD.Load<PackedScene>("res://Scenes/Dungeon/Chest.tscn");
			if (chestScene != null)
			{
				_challengeChest = chestScene.Instantiate<DungeonChest>();
				_challengeChest.GoldAmount = 50; // High value reward!
				LockChestUntilRoomCleared(_challengeChest);
				AddChild(_challengeChest);
				_challengeChest.Position = Vector2.Zero;
				_challengeChest.Visible = true;
			}

			// 3. Dungeon Key placed in a distant corner on a pedestal
			var keyScene = GD.Load<PackedScene>("res://Scenes/Dungeon/Key.tscn");
			if (keyScene != null)
			{
				_dungeonKey = keyScene.Instantiate<DungeonKey>();
				AddChild(_dungeonKey);

				// Place in far quadrant (e.g. top-left or bottom-right)
				float halfW = data.WorldBounds.Size.X * 0.38f;
				float halfH = data.WorldBounds.Size.Y * 0.38f;
				_dungeonKey.Position = new Vector2(-halfW, -halfH);

				_dungeonKey.Collected += OnChallengeKeyCollected;
			}
		}

		private void SetupMazeRoom(DungeonRoomData data)
		{
			// Place a reward chest in the maze
			var chestScene = GD.Load<PackedScene>("res://Scenes/Dungeon/Chest.tscn");
			if (chestScene != null)
			{
				var chest = chestScene.Instantiate<DungeonChest>();
				chest.GoldAmount = 30;
				LockChestUntilRoomCleared(chest);
				AddChild(chest);
				chest.Position = new Vector2(data.WorldBounds.Size.X * 0.3f, data.WorldBounds.Size.Y * 0.3f);
			}
		}

		private void SetupShopRoom(DungeonRoomData data)
		{
			var shop = new ShopRoom { Name = "ShopRoom" };
			AddChild(shop);
			shop.Position = Vector2.Zero;
			shop.Setup(data.WorldBounds.Size);
		}

		private void LockChestUntilRoomCleared(DungeonChest chest)
		{
			chest.LockUntilRoomCleared();
			_roomClearLockedChests.Add(chest);
		}

		private void OnTriggerBodyEntered(Node2D body)
		{
			if (!(body is Player player))
				return;

			if (Data == null || Data.State == RoomState.Cleared)
				return;

			if (Data.State == RoomState.Unvisited)
			{
				Data.State = RoomState.Active;
				PlayerEntered?.Invoke(this);

				if (Data.Type == RoomType.Combat || Data.Type == RoomType.BossExit)
				{
					LockDoorsAfterEntry(player);
					SpawnWave();
				}
				else if (Data.Type == RoomType.Challenge)
				{
					LockDoorsAfterEntry(player);
					StartChallenge();
				}
				else if (Data.Type == RoomType.Maze)
				{
					// Maze room can spawn light encounters or explore freely
					SpawnMazeEnemies();
				}
				else if (Data.Type == RoomType.Spawn || Data.Type == RoomType.Shop)
				{
					Data.State = RoomState.Cleared;
					UnlockDoors();
					RoomCleared?.Invoke(this);
				}
			}
		}

		private void StartChallenge()
		{
			_challengeActive = true;
			_challengeTimer = 35.0f;
			ChallengeTimerUpdated?.Invoke(_challengeTimer, false);
		}

		private void OnChallengeKeyCollected()
		{
			_challengeCompleted = true;
			_challengeActive = false;

			if (_poisonGas != null)
			{
				_poisonGas.Disperse();
			}

			Data.State = RoomState.Cleared;
			UnlockDoors();
			foreach (var chest in _roomClearLockedChests)
			{
				if (GodotObject.IsInstanceValid(chest))
					chest.MarkRoomCleared();
			}

			ChallengeTimerUpdated?.Invoke(0.0f, false);
			RoomCleared?.Invoke(this);
		}

		private void LockDoorsAfterEntry(Player player)
		{
			_playerEntering = player;
			_entryDoor = null;
			float nearestDistance = float.MaxValue;
			foreach (var door in Doors)
			{
				float distance = player.GlobalPosition.DistanceSquaredTo(door.GlobalPosition);
				if (distance < nearestDistance)
				{
					nearestDistance = distance;
					_entryDoor = door;
				}
			}

			// The trigger begins just inside the room while the player may still overlap the doorway.
			// Keep all doors open until the player has moved clear of the entrance.
			_pendingDoorLock = _entryDoor != null && nearestDistance < 64.0f * 64.0f;
			if (!_pendingDoorLock)
				LockDoors();
		}

		public void LockDoors()
		{
			foreach (var door in Doors)
			{
				door.Close();
			}
		}

		public void UnlockDoors()
		{
			foreach (var door in Doors)
			{
				door.Open();
			}
		}

		private void SpawnWave()
		{
			if (_waveSpawned)
				return;

			_waveSpawned = true;
			ActiveEnemies.Clear();

			var dm = GetTree().CurrentScene?.GetNodeOrNull<DungeonManager>("DungeonManager");
			int floor = dm != null ? dm.CurrentFloor : 1;
			float hpMult = 1.0f + (floor - 1) * 0.35f;
			float dmgMult = 1.0f + (floor - 1) * 0.20f;

			int count = Data.Type == RoomType.BossExit ? (6 + floor) : (int)GD.RandRange(3 + floor, 5 + floor);
			var rng = new Random();

			for (int i = 0; i < count; i++)
			{
				string path;
				if (Data.Type == RoomType.BossExit && i == 0)
				{
					path = "res://Scenes/Enemies/OrcEnemy.tscn";
				}
				else
				{
					int idx = rng.Next(EnemyScenePaths.Length);
					path = EnemyScenePaths[idx];
				}

				var packed = GD.Load<PackedScene>(path);
				if (packed == null)
					continue;

				Enemy enemy = packed.Instantiate<Enemy>();
				GetTree().CurrentScene.AddChild(enemy);

				// Apply floor scaling
				enemy.MaxHealth *= hpMult;
				enemy.AttackDamage *= dmgMult;
				if (Data.Type == RoomType.BossExit && i == 0)
				{
					// Boss buff
					enemy.MaxHealth *= 2.0f;
					enemy.AttackDamage *= 1.3f;
					enemy.Scale = new Vector2(1.4f, 1.4f);
				}

				float halfW = (Data.WorldBounds.Size.X * 0.35f);
				float halfH = (Data.WorldBounds.Size.Y * 0.35f);
				Vector2 offset = new Vector2(
					(float)GD.RandRange(-halfW, halfW),
					(float)GD.RandRange(-halfH, halfH)
				);

				enemy.GlobalPosition = Data.WorldCenter + offset;
				ActiveEnemies.Add(enemy);
			}
		}

		private void SpawnMazeEnemies()
		{
			ActiveEnemies.Clear();
			for (int i = 0; i < 3; i++)
			{
				var packed = GD.Load<PackedScene>("res://Scenes/Enemies/SkeletonEnemy.tscn");
				if (packed == null) continue;

				Enemy enemy = packed.Instantiate<Enemy>();
				GetTree().CurrentScene.AddChild(enemy);
				enemy.GlobalPosition = Data.WorldCenter + new Vector2(
					(float)GD.RandRange(-60, 60),
					(float)GD.RandRange(-60, 60)
				);
				ActiveEnemies.Add(enemy);
			}
		}

		public override void _PhysicsProcess(double delta)
		{
			float dt = (float)delta;

			if (Data == null)
				return;

			if (_pendingDoorLock)
			{
				if (!GodotObject.IsInstanceValid(_playerEntering) || !GodotObject.IsInstanceValid(_entryDoor) ||
					_playerEntering.GlobalPosition.DistanceSquaredTo(_entryDoor.GlobalPosition) >= 48.0f * 48.0f)
				{
					_pendingDoorLock = false;
					LockDoors();
				}
			}

			// Handle Challenge Room Timer
			if (Data.Type == RoomType.Challenge && _challengeActive && !_challengeCompleted)
			{
				_challengeTimer -= dt;
				bool gasSpread = _challengeTimer <= 0.0f;

				if (gasSpread && _poisonGas != null && !_poisonGas.IsActive)
				{
					_poisonGas.Activate(0.4f);
				}

				ChallengeTimerUpdated?.Invoke(Mathf.Max(_challengeTimer, 0.0f), gasSpread);
			}

			// Handle Combat / Boss Rooms
			if (Data.State == RoomState.Active)
			{
				if (Data.Type == RoomType.Combat || Data.Type == RoomType.BossExit || Data.Type == RoomType.Maze)
				{
					if (_waveSpawned || Data.Type == RoomType.Maze)
					{
						ActiveEnemies.RemoveAll(e => !GodotObject.IsInstanceValid(e) || e.IsDead());

						if (ActiveEnemies.Count == 0 && Data.State != RoomState.Cleared)
						{
							OnRoomCleared();
						}
					}
				}
			}
		}

		private void OnRoomCleared()
		{
			Data.State = RoomState.Cleared;
			UnlockDoors();
			foreach (var chest in _roomClearLockedChests)
			{
				if (GodotObject.IsInstanceValid(chest))
					chest.MarkRoomCleared();
			}

			if (Data.Type == RoomType.Combat)
			{
				var chestScene = GD.Load<PackedScene>("res://Scenes/Dungeon/Chest.tscn");
				if (chestScene != null && GetTree().CurrentScene != null)
				{
					var chest = chestScene.Instantiate<DungeonChest>();
					chest.GlobalPosition = Data.WorldCenter;
					GetTree().CurrentScene.AddChild(chest);
				}
			}

			if (_portal != null)
			{
				_portal.Activate();
			}

			RoomCleared?.Invoke(this);
		}
	}
}
