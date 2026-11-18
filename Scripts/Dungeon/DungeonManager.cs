using Godot;
using System;
using System.Collections.Generic;

namespace RogueBound.Dungeon
{
	public partial class DungeonManager : Node
	{
		[Export] public int FloorNumber = 1;
		[Export] public PackedScene UpgradeScene;

		public event Action<int, int> ProgressChanged;
		public event Action<int> EnemiesCountChanged;
		public event Action<string, string> ChallengeStatusChanged;
		public event Action<string> ChallengeNotice;

		private DungeonGenerator _generator;
		private TileMapLayer _tileMapLayer;
		private Player _player;
		private DungeonMinimap _minimap;
		private CanvasLayer _minimapLayer;
		private DungeonRoom _currentRoom;
		private bool _endingTriggered;

		private int _clearedRoomsCount = 0;
		private int _totalCombatRooms = 0;
		private readonly HashSet<ulong> _countedClearedRooms = new();

		public int CurrentFloor => FloorNumber;
		public int ClearedRooms => _clearedRoomsCount;
		public int TotalCombatRooms => _totalCombatRooms;

		public override void _Ready()
		{
			// Initialize database run
			DatabaseManager database = GetNodeOrNull<DatabaseManager>("/root/DatabaseManager");
			database?.StartRun();

			_generator = GetNodeOrNull<DungeonGenerator>("DungeonGenerator");
			if (_generator == null)
			{
				_generator = new DungeonGenerator { Name = "DungeonGenerator" };
				AddChild(_generator);
			}

			CallDeferred(nameof(InitializeDungeon));
		}

		private void InitializeDungeon()
		{
			_tileMapLayer = GetTree().CurrentScene.GetNodeOrNull<TileMapLayer>("TileMap/Layer1")
				?? GetTree().CurrentScene.GetNodeOrNull<TileMapLayer>("Layer1");

			if (_tileMapLayer == null)
			{
				GD.PushError("DungeonManager: Could not find TileMapLayer!");
				return;
			}

			// Generate layout
			_generator.GenerateLayout(FloorNumber);

			// Carve tiles and build rooms/doors
			_generator.BuildDungeonTiles(_tileMapLayer, this);

			// Count combat rooms
			_totalCombatRooms = 0;
			foreach (var roomNode in _generator.InstantiatedRooms)
			{
				if (IsCombatRoom(roomNode.Data.Type))
					_totalCombatRooms++;

				roomNode.PlayerEntered += OnPlayerEnteredRoom;
				roomNode.RoomCleared += OnRoomCleared;
				roomNode.ChallengeStatusChanged += (title, hint) => ChallengeStatusChanged?.Invoke(title, hint);
				roomNode.ChallengeNotice += message => ChallengeNotice?.Invoke(message);
			}

			// Spawn player at center of spawn room
			_player = GetTree().GetFirstNodeInGroup("player") as Player;
			if (_player != null && _generator.SpawnRoomData != null)
			{
				_player.GlobalPosition = _generator.SpawnRoomData.WorldCenter;
				_player.Died -= OnPlayerDied;
				_player.Died += OnPlayerDied;

				// Camera smoothing for dungeon exploration
				var cam = _player.GetNodeOrNull<Camera2D>("Camera2D");
				if (cam != null)
				{
					cam.PositionSmoothingEnabled = true;
					cam.PositionSmoothingSpeed = 8.0f;
				}
			}

			// Setup minimap
			AttachMinimap();
			GetNodeOrNull<DatabaseManager>("/root/DatabaseManager")?.UpdateDungeonProgress(FloorNumber);

			ProgressChanged?.Invoke(_clearedRoomsCount, _totalCombatRooms);
			SpawnFirstFloorTutorial();
		}

		private void SpawnFirstFloorTutorial()
		{
			if (FloorNumber != 1)
				return;

			var database = GetNodeOrNull<DatabaseManager>("/root/DatabaseManager");
			long saveId = database?.CurrentSaveId ?? -1;
			if (saveId < 0 || !database.NeedsTutorial(saveId))
				return;

			PackedScene overlayScene = GD.Load<PackedScene>("res://Scenes/Menus/TutorialOverlay.tscn");
			if (overlayScene == null)
			{
				GD.PushError("TutorialOverlay.tscn could not be loaded.");
				return;
			}

			var overlay = overlayScene.Instantiate<TutorialOverlay>();
			overlay.Continued += () => database.CompleteTutorial(saveId);
			GetTree().CurrentScene.AddChild(overlay);
		}

		private void AttachMinimap()
		{
			// Replace the previous floor's map before adding the new one. QueueFree()
			// leaves the old CanvasLayer visible until the end of the frame, so a
			// floor transition can briefly show both maps in the same position.
			if (GodotObject.IsInstanceValid(_minimapLayer))
				_minimapLayer.Free();

			_minimapLayer = new CanvasLayer { Name = "MinimapLayer" };
			AddChild(_minimapLayer);

			var container = new MarginContainer();
			container.SetAnchorsPreset(Control.LayoutPreset.TopRight);
			container.OffsetLeft = -150;
			container.OffsetTop = 15;
			container.OffsetRight = -15;
			container.OffsetBottom = 150;
			_minimapLayer.AddChild(container);

			_minimap = new DungeonMinimap();
			container.AddChild(_minimap);
			_minimap.Initialize(_generator.RoomMap, _generator.SpawnRoomData.GridCoord);
		}

		private void OnPlayerEnteredRoom(DungeonRoom room)
		{
			_currentRoom = room;
			_minimap?.SetCurrentRoom(room.Data.GridCoord);

			if (IsCombatRoom(room.Data.Type))
			{
				EnemiesCountChanged?.Invoke(room.ActiveEnemies.Count);
			}
			else
			{
				EnemiesCountChanged?.Invoke(0);
			}
		}

		private void OnRoomCleared(DungeonRoom room)
		{
			if (IsCombatRoom(room.Data.Type) &&
				_countedClearedRooms.Add(room.GetInstanceId()))
			{
				_clearedRoomsCount++;
				GetNodeOrNull<DatabaseManager>("/root/DatabaseManager")?.UpdateDungeonProgress(FloorNumber, true);
			}

			ProgressChanged?.Invoke(_clearedRoomsCount, _totalCombatRooms);
			EnemiesCountChanged?.Invoke(0);
			if (room.Data.Type == RoomType.OrcKing)
				TryTriggerEnding(true);
		}

		private void OnPlayerDied()
		{
			if (_endingTriggered)
				return;
			CleanupAllBossEffects();
			DatabaseManager database = GetNodeOrNull<DatabaseManager>("/root/DatabaseManager");
			int kills = database?.GetCurrentRunKillCount() ?? 0;
			bool diedToOrcKing = FloorNumber == BossRoster.TOTAL_FLOORS &&
				_currentRoom?.Data.Type == RoomType.OrcKing &&
				_currentRoom.Data.State == RoomState.Active &&
				_player?.LastDamageSource is OrcKing;
			if (diedToOrcKing)
			{
				TryTriggerEnding(false);
				return;
			}
			database?.FinishRun("Defeated");

			var gameOverScene = GD.Load<PackedScene>("res://Scenes/Menus/GameOverMenu.tscn");
			if (gameOverScene != null && GetTree()?.CurrentScene != null)
			{
				GameOverMenu menu = gameOverScene.Instantiate<GameOverMenu>();
				GetTree().CurrentScene.AddChild(menu);
				menu.Setup(false, _clearedRoomsCount, _totalCombatRooms, kills, _player?.Money ?? 0);
			}
		}

		public void NextFloor()
		{
			if (FloorNumber >= BossRoster.TOTAL_FLOORS)
			{
				bool finalBossCleared = false;
				foreach (DungeonRoom room in _generator.InstantiatedRooms)
					if (room.Data.Type == RoomType.OrcKing && room.Data.State == RoomState.Cleared)
						finalBossCleared = true;
				if (finalBossCleared)
				{
					TryTriggerEnding(true);
				}
				return;
			}

			var summary = new FloorSummaryMenu();
			GetTree().CurrentScene.AddChild(summary);
			summary.Setup(FloorNumber, _clearedRoomsCount, _totalCombatRooms,
				GetNodeOrNull<DatabaseManager>("/root/DatabaseManager")?.GetCurrentRunKillCount() ?? 0,
				_player?.Money ?? 0);
			summary.ContinueRequested += OnFloorSummaryContinue;
			GetTree().Paused = true;
		}

		private void OnFloorSummaryContinue()
		{
			if (UpgradeScene != null && GetTree()?.CurrentScene != null)
			{
				var upgradeLayer = UpgradeScene.Instantiate<CanvasLayer>();
				Control upgradeControl = upgradeLayer.GetNodeOrNull<Control>("UpgradeScene");
				if (upgradeControl != null)
				{
					upgradeLayer.ProcessMode = Node.ProcessModeEnum.Always;
					upgradeControl.TreeExiting += () =>
					{
						upgradeLayer.QueueFree();
						OnUpgradeSelectionFinished();
					};
					GetTree().CurrentScene.AddChild(upgradeLayer);
					return;
				}

				GD.PushError("DungeonManager: UpgradeScene does not contain an UpgradeScene Control!");
				upgradeLayer.QueueFree();
			}

			GetTree().Paused = false;
			AdvanceFloor();
		}

		private void OnUpgradeSelectionFinished()
		{
			GetTree().Paused = false;
			AdvanceFloor();
		}

		private void AdvanceFloor()
		{
			FloorNumber++;
			RebuildFloor();
		}

		public void DebugGoToFloor(int floor)
		{
			if (_generator == null || !GodotObject.IsInstanceValid(_player) || _player.health <= 0)
				return;
			FloorNumber = Mathf.Max(1, floor);
			RebuildFloor();
		}

		private void RebuildFloor()
		{
			_currentRoom = null;
			foreach (Node enemy in GetTree().GetNodesInGroup("enemies"))
				enemy.Free();
			foreach (Node item in GetTree().GetNodesInGroup("floor_items"))
				item.Free();

			GD.Print($"[DungeonManager] Loading Floor {FloorNumber}!");

			// Clean up previous rooms & doors
			foreach (var room in _generator.InstantiatedRooms)
			{
				if (GodotObject.IsInstanceValid(room))
				{
					foreach (var door in room.Doors)
					{
						if (GodotObject.IsInstanceValid(door))
							door.Free();
					}
					room.Free();
				}
			}

			if (GodotObject.IsInstanceValid(_minimapLayer))
				_minimapLayer.Free();
			_minimapLayer = null;
			_minimap = null;

			_clearedRoomsCount = 0;
			_countedClearedRooms.Clear();
			InitializeDungeon();
		}

		public override void _Process(double delta)
		{
			if (_currentRoom?.Data.State == RoomState.Active && IsCombatRoom(_currentRoom.Data.Type))
			{
				EnemiesCountChanged?.Invoke(_currentRoom.ActiveEnemies.Count);
			}
		}

		private static bool IsCombatRoom(RoomType type) =>
			type == RoomType.Combat || type == RoomType.BossExit || type == RoomType.GolemBoss ||
			type == RoomType.IvernBoss || type == RoomType.OrcKing;

		private void CleanupAllBossEffects()
		{
			_player?.ClearRootEffect();
			foreach (Node node in GetTree().GetNodesInGroup("enemies"))
				if (node is Enemy enemy && enemy is IBossEncounter boss && GodotObject.IsInstanceValid(enemy))
				{
					boss.SetRoomActive(false);
					boss.HideBossBar();
					boss.CleanupBossEffects();
				}
			foreach (Node node in GetTree().GetNodesInGroup("ivern_effects"))
				if (GodotObject.IsInstanceValid(node))
					node.QueueFree();
			foreach (Node node in GetTree().GetNodesInGroup("golem_rocks"))
				if (GodotObject.IsInstanceValid(node))
					node.QueueFree();
		}

		private void TryTriggerEnding(bool happy)
		{
			if (_endingTriggered)
				return;
			_endingTriggered = true;
			GetNodeOrNull<DatabaseManager>("/root/DatabaseManager")?.FinishRun(happy ? "Victory" : "Defeated");
			CallDeferred(nameof(LoadEnding), happy);
		}

		private void LoadEnding(bool happy)
			=> GetTree().ChangeSceneToFile(happy ? "res://Scenes/Endings/VictoryEnding.tscn" : "res://Scenes/Endings/DefeatEnding.tscn");

		public override void _UnhandledInput(InputEvent @event)
		{
			if (!OS.IsDebugBuild() || _endingTriggered || @event is not InputEventKey key || !key.Pressed || key.Echo)
				return;
			if (key.Keycode == Key.F9)
				TryTriggerEnding(true);
			else if (key.Keycode == Key.F10)
				TryTriggerEnding(false);
			else return;
			GetViewport().SetInputAsHandled();
		}
	}
}
