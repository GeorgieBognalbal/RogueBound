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
		public event Action<string, string> ChallengeStatusChanged;
		public event Action<string> ChallengeNotice;

		private Area2D _triggerArea;
		private CollisionShape2D _triggerCollision;
		private DungeonPortal _portal;
		private DungeonChest _challengeChest;
		private readonly int[] _sequence = { 0, 1, 2 };
		private readonly Area2D[] _plates = new Area2D[3];
		private const string Runes = "ABC";
		private int _matched;
		private float _previewLeft;
		private float _resetHintLeft;
		private PoisonGas _poisonGas;

		private bool _waveSpawned = false;
		private bool _challengeActive = false;
		private bool _challengeGasStarted = false;
		private float _challengeTimer = 35.0f;
		private const float ChallengeGasStartTime = 10.0f;
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
				_triggerArea.BodyExited += OnTriggerBodyExited;
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
				_triggerArea.BodyExited += OnTriggerBodyExited;
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
				SetupPlateMaze(data);
			}
			else if (data.Type == RoomType.Shop)
			{
				SetupShopRoom(data);
			}
		}

		private void SetupPlateMaze(DungeonRoomData data)
		{
			_poisonGas = GD.Load<PackedScene>("res://Scenes/Dungeon/PoisonGas.tscn").Instantiate<PoisonGas>();
			AddChild(_poisonGas);
			_poisonGas.SetupBounds(data.WorldBounds.Size);

			var chestScene = GD.Load<PackedScene>("res://Scenes/Dungeon/Chest.tscn");
			if (chestScene != null)
			{
				_challengeChest = chestScene.Instantiate<DungeonChest>();
				_challengeChest.GoldAmount = 50;
				_challengeChest.LockUntilRoomCleared();
				AddChild(_challengeChest);
				_challengeChest.Position = Vector2.Zero;
				_challengeChest.Visible = true;
			}

			Vector2[] corners = { new(-0.34f, -0.34f), new(0.34f, -0.34f), new(0.34f, 0.34f) };
			var plateScene = GD.Load<PackedScene>("res://Scenes/Dungeon/PressurePlate.tscn");
			for (int i = 0; i < _plates.Length; i++)
			{
				int rune = i;
				var plate = _plates[i] = plateScene.Instantiate<Area2D>();
				plate.Position = corners[i] * data.WorldBounds.Size;
				plate.GetNode<Label>("Rune").Text = Runes[i].ToString();
				plate.BodyEntered += body => { if (body is Player) StepOnPlate(rune); };
				AddChild(plate);
			}
		}

		private void SetupShopRoom(DungeonRoomData data)
		{
			var shop = new ShopRoom { Name = "ShopRoom" };
			AddChild(shop);
			shop.Position = Vector2.Zero;
			shop.Setup(data.WorldBounds.Size);
		}

		private void OnTriggerBodyEntered(Node2D body)
		{
			if (!(body is Player player))
				return;

			// Re-entering a boss room after stepping out restores its HUD without replaying the intro.
			foreach (Enemy enemy in ActiveEnemies)
				if (enemy is IBossEncounter activeBoss && GodotObject.IsInstanceValid(enemy))
				{
					activeBoss.SetRoomActive(true);
					activeBoss.ShowBossBarIfFightStarted();
				}

			if (Data == null || Data.State == RoomState.Cleared)
				return;

			PlayerEntered?.Invoke(this);
			if (Data.State == RoomState.Unvisited)
			{
				Data.State = RoomState.Active;

				if (IsCombatRoom(Data.Type))
				{
					LockDoorsAfterEntry(player);
					if (Data.Type == RoomType.GolemBoss || Data.Type == RoomType.IvernBoss || Data.Type == RoomType.OrcKing)
						player.BeginBossIntroLock();
					CallDeferred(nameof(SpawnWave));
				}
				else if (Data.Type == RoomType.Challenge)
				{
					LockDoorsAfterEntry(player);
					StartChallenge();
				}
				else if (Data.Type == RoomType.Spawn || Data.Type == RoomType.Shop)
				{
					Data.State = RoomState.Cleared;
					UnlockDoors();
					RoomCleared?.Invoke(this);
				}
			}
		}

		private void OnTriggerBodyExited(Node2D body)
		{
			if (body is not Player) return;
			foreach (Enemy enemy in ActiveEnemies)
				if (enemy is IBossEncounter boss && GodotObject.IsInstanceValid(enemy))
				{
					boss.SetRoomActive(false);
					boss.HideBossBar();
					boss.CleanupBossEffects();
				}
		}

		private void StartChallenge()
		{
			_challengeActive = true;
			for (int i = _sequence.Length - 1; i > 0; i--)
			{
				int j = GD.RandRange(0, i);
				(_sequence[i], _sequence[j]) = (_sequence[j], _sequence[i]);
			}
			_previewLeft = _plates.Length;
			ChallengeStatusChanged?.Invoke($"MEMORIZE   {Runes[_sequence[0]]} - {Runes[_sequence[1]]} - {Runes[_sequence[2]]}",
				"Then step on the plates in order");
			ShowPreviewPlate(0);
		}

		private void ShowPreviewPlate(int index)
		{
			for (int i = 0; i < _plates.Length; i++)
				_plates[_sequence[i]].Modulate = i == index ? Colors.Gold : Colors.LightGray;
		}

		private void StepOnPlate(int rune)
		{
			if (!_challengeActive || _previewLeft > 0)
				return;

			if (rune != _sequence[_matched])
			{
				_matched = 0;
				_resetHintLeft = 1.5f;
				ShowPreviewPlate(-1);
			}
			else
			{
				_resetHintLeft = 0;
				_plates[rune].Modulate = Colors.LightGreen;
				if (++_matched == _plates.Length)
				{
					FinishChallenge(true);
					return;
				}
			}
			UpdatePlateHud();
		}

		private void UpdatePlateHud()
		{
			string hint = _resetHintLeft > 0 ? "Wrong plate! Start the sequence again" : "Step on the plates in order";
			ChallengeStatusChanged?.Invoke($"PLATES {_matched}/{_plates.Length}   {Mathf.CeilToInt(_challengeTimer)}s",
				_challengeGasStarted ? $"Gas!  {hint}" : hint);
		}

		private void FinishChallenge(bool solved)
		{
			_challengeActive = false;
			_poisonGas?.Disperse();
			if (solved)
				_challengeChest?.MarkRoomCleared();
			else
			{
				_challengeChest?.Disappear();
				foreach (var plate in _plates)
					plate.Modulate = Colors.DimGray;
			}
			OnRoomCleared();
			ChallengeNotice?.Invoke(solved ? "CHEST UNLOCKED!" : "FAILED! TIME UP — CHEST LOST");
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
				CallDeferred(nameof(LockDoors));
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
			_pendingDoorLock = false;
			foreach (var door in Doors)
			{
				door.Open();
			}
		}

		private void SpawnWave()
		{
			if (_waveSpawned)
				return;

			ActiveEnemies.Clear();

			var dm = GetTree().CurrentScene?.GetNodeOrNull<DungeonManager>("DungeonManager");
			int floor = dm != null ? dm.CurrentFloor : 1;
			float hpMult = 1.0f + (floor - 1) * 0.35f;
			float dmgMult = 1.0f + (floor - 1) * 0.20f;

			if (Data.Type == RoomType.GolemBoss || Data.Type == RoomType.IvernBoss || Data.Type == RoomType.OrcKing)
			{
				string bossPath = Data.Type switch
				{
					RoomType.GolemBoss => "res://Scenes/Enemies/GolemMiniboss.tscn",
					RoomType.IvernBoss => "res://Scenes/Enemies/IvernMiniboss.tscn",
					_ => "res://Scenes/Enemies/OrcKing.tscn",
				};
				var bossScene = GD.Load<PackedScene>(bossPath);
				if (bossScene == null)
				{
					GD.PushError($"Boss scene could not be loaded: {bossPath}");
					(GetTree().GetFirstNodeInGroup("player") as Player)?.EndBossIntroLock();
					return;
				}

				Enemy bossEnemy = bossScene.Instantiate<Enemy>();
				if (Data.Type == RoomType.GolemBoss)
				{
					bossEnemy.MaxHealth = GolemBossSettings.BOSS_BASE_HEALTH * hpMult;
					bossEnemy.AttackDamage = (int)(GolemBossSettings.BOSS_BASE_CONTACT_DAMAGE * dmgMult);
				}
				else if (Data.Type == RoomType.IvernBoss)
				{
					bossEnemy.MaxHealth = BossRoster.IVERN_MAX_HP * hpMult;
					bossEnemy.AttackDamage = (int)(BossRoster.IVERN_CONTACT_DAMAGE * dmgMult);
				}
				else
				{
					bossEnemy.MaxHealth = BossRoster.ORC_KING_MAX_HP * hpMult;
					bossEnemy.AttackDamage = BossRoster.ORC_KING_SLASH_DAMAGE * dmgMult;
				}
				bossEnemy.GlobalPosition = Data.WorldCenter;
				GetTree().CurrentScene.AddChild(bossEnemy);
				((IBossEncounter)bossEnemy).StartBossIntroOnce(GetTree().GetFirstNodeInGroup("player") as Player);
				ActiveEnemies.Add(bossEnemy);
				_waveSpawned = true;
				return;
			}

			// The exit room remains a normal encounter; boss fights only come from
			// the explicit BossRoster floor table.
			int count = (int)GD.RandRange(3 + floor, 5 + floor);
			var rng = new Random();

			for (int i = 0; i < count; i++)
			{
				int idx = rng.Next(EnemyScenePaths.Length);
				string path = EnemyScenePaths[idx];

				var packed = GD.Load<PackedScene>(path);
				if (packed == null)
					continue;

				Enemy enemy = packed.Instantiate<Enemy>();
				// Apply floor scaling
				enemy.MaxHealth *= hpMult;
				enemy.AttackDamage *= dmgMult;
				float halfW = (Data.WorldBounds.Size.X * 0.25f);
				float halfH = (Data.WorldBounds.Size.Y * 0.25f);
				Vector2 offset = new Vector2(
					(float)GD.RandRange(-halfW, halfW),
					(float)GD.RandRange(-halfH, halfH)
				);

				enemy.GlobalPosition = Data.WorldCenter + offset;
				GetTree().CurrentScene.AddChild(enemy);
				ActiveEnemies.Add(enemy);
			}
			_waveSpawned = ActiveEnemies.Count > 0;
			if (!_waveSpawned)
				GD.PushError($"No enemies spawned in {Name}. Room remains uncleared.");
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

			KeepActiveEnemiesInsideRoom();

			// Handle Challenge Room Timer
			if (_challengeActive)
			{
				if (_previewLeft > 0)
				{
					int previous = Mathf.CeilToInt(_previewLeft);
					_previewLeft = Mathf.Max(0, _previewLeft - dt);
					int left = Mathf.CeilToInt(_previewLeft);
					if (left != previous)
						ShowPreviewPlate(_plates.Length - left);
					if (left == 0)
						UpdatePlateHud();
					return;
				}

				int seconds = Mathf.CeilToInt(_challengeTimer);
				bool hadResetHint = _resetHintLeft > 0;
				_resetHintLeft -= dt;
				_challengeTimer -= dt;
				if (_challengeTimer <= 0.0f)
				{
					FinishChallenge(false);
					return;
				}

				bool gasSpread = _challengeTimer <= ChallengeGasStartTime;

				if (gasSpread && _poisonGas != null && !_challengeGasStarted)
				{
					_challengeGasStarted = true;
					_poisonGas.Activate(0.4f);
				}

				if (seconds != Mathf.CeilToInt(_challengeTimer) || (hadResetHint && _resetHintLeft <= 0))
					UpdatePlateHud();
			}

			// Handle Combat / Boss Rooms
			if (Data.State == RoomState.Active)
			{
				if (IsCombatRoom(Data.Type))
				{
					if (_waveSpawned)
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

		private void KeepActiveEnemiesInsideRoom()
		{
			if (Data == null)
				return;

			// Enemies may pursue the player, but they must not follow through an open doorway
			// into another room while the entrance door is waiting to close.
			const float wallInset = 32.0f;
			Rect2 bounds = Data.WorldBounds;
			float minX = bounds.Position.X + wallInset;
			float maxX = bounds.End.X - wallInset;
			float minY = bounds.Position.Y + wallInset;
			float maxY = bounds.End.Y - wallInset;
			foreach (Enemy enemy in ActiveEnemies)
			{
				if (!GodotObject.IsInstanceValid(enemy) || enemy.IsDead())
					continue;

				Vector2 position = enemy.GlobalPosition;
				Vector2 contained = new(
					Mathf.Clamp(position.X, minX, maxX),
					Mathf.Clamp(position.Y, minY, maxY));
				if (contained != position)
					enemy.GlobalPosition = contained;
			}
		}

		private void OnRoomCleared()
		{
			Data.State = RoomState.Cleared;
			UnlockDoors();

			if (Data.Type == RoomType.Combat || Data.Type == RoomType.GolemBoss || Data.Type == RoomType.IvernBoss)
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
				int currentFloor = GetTree().CurrentScene?.GetNodeOrNull<DungeonManager>("DungeonManager")?.CurrentFloor ?? 1;
				if (Data.Type != RoomType.BossExit || currentFloor < BossRoster.TOTAL_FLOORS)
					_portal.Activate();
			}

			RoomCleared?.Invoke(this);
		}

		private static bool IsCombatRoom(RoomType type) =>
			type == RoomType.Combat || type == RoomType.BossExit || type == RoomType.GolemBoss ||
			type == RoomType.IvernBoss || type == RoomType.OrcKing;
	}
}
