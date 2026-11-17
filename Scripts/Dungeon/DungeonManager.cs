using Godot;
using System;
using System.Collections.Generic;

namespace RogueBound.Dungeon
{
	public partial class DungeonManager : Node
	{
		[Export] public int FloorNumber = 1;
		[Export] public int MaxFloors = 3;
		[Export] public PackedScene UpgradeScene;

		public event Action<int, int> ProgressChanged;
		public event Action<int> EnemiesCountChanged;
		public event Action<float, bool> ChallengeTimerUpdated;
		public event Action<string> ChallengeNotice;

		private DungeonGenerator _generator;
		private TileMapLayer _tileMapLayer;
		private Player _player;
		private DungeonMinimap _minimap;
		private CanvasLayer _minimapLayer;
		private DungeonRoom _currentRoom;

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
			_generator.GenerateLayout();

			// Carve tiles and build rooms/doors
			_generator.BuildDungeonTiles(_tileMapLayer, this);

			// Count combat rooms
			_totalCombatRooms = 0;
			foreach (var roomNode in _generator.InstantiatedRooms)
			{
				if (roomNode.Data.Type == RoomType.Combat || roomNode.Data.Type == RoomType.BossExit)
					_totalCombatRooms++;

				roomNode.PlayerEntered += OnPlayerEnteredRoom;
				roomNode.RoomCleared += OnRoomCleared;
				roomNode.ChallengeTimerUpdated += (rem, gas) => ChallengeTimerUpdated?.Invoke(rem, gas);
				roomNode.ChallengeNotice += message => ChallengeNotice?.Invoke(message);
			}

			// Spawn player at center of spawn room
			_player = GetTree().GetFirstNodeInGroup("player") as Player;
			if (_player != null && _generator.SpawnRoomData != null)
			{
				_player.GlobalPosition = _generator.SpawnRoomData.WorldCenter;
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

			ProgressChanged?.Invoke(_clearedRoomsCount, _totalCombatRooms);
			SpawnFirstFloorTutorial();
		}

		private void SpawnFirstFloorTutorial()
		{
			if (FloorNumber != 1)
				return;

			var settings = new ConfigFile();
			settings.Load("user://roguebound_settings.cfg");
			int tutorialStateVersion = settings.GetValue("tutorial", "state_version", 0).AsInt32();
			if (tutorialStateVersion < 3)
			{
				// Re-show the current tutorial once for profiles that completed an older version.
				settings.SetValue("tutorial", "tutorial_done", false);
				settings.SetValue("tutorial", "state_version", 3);
				Error saveResult = settings.Save("user://roguebound_settings.cfg");
				if (saveResult != Error.Ok)
					GD.PushWarning($"Could not initialize tutorial progress: {saveResult}");
			}

			bool tutorialDone = settings.GetValue("tutorial", "tutorial_done", false).AsBool();
			if (tutorialDone)
				return;

			PackedScene overlayScene = GD.Load<PackedScene>("res://Scenes/Menus/TutorialOverlay.tscn");
			if (overlayScene == null)
			{
				GD.PushError("TutorialOverlay.tscn could not be loaded.");
				return;
			}

			GetTree().CurrentScene.AddChild(overlayScene.Instantiate<CanvasLayer>());
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

			if (room.Data.Type == RoomType.Combat || room.Data.Type == RoomType.BossExit)
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
			if ((room.Data.Type == RoomType.Combat || room.Data.Type == RoomType.BossExit) &&
				_countedClearedRooms.Add(room.GetInstanceId()))
			{
				_clearedRoomsCount++;
			}

			ProgressChanged?.Invoke(_clearedRoomsCount, _totalCombatRooms);
			EnemiesCountChanged?.Invoke(0);
		}

		private void OnPlayerDied()
		{
			DatabaseManager database = GetNodeOrNull<DatabaseManager>("/root/DatabaseManager");
			int kills = database?.GetCurrentRunKillCount() ?? 0;
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
			if (FloorNumber >= MaxFloors)
			{
				// Victory achieved!
				DatabaseManager db = GetNodeOrNull<DatabaseManager>("/root/DatabaseManager");
				int kills = db?.GetCurrentRunKillCount() ?? 0;
				db?.FinishRun("Victory");

				var victoryScene = GD.Load<PackedScene>("res://Scenes/Menus/GameOverMenu.tscn");
				if (victoryScene != null && GetTree()?.CurrentScene != null)
				{
					GameOverMenu menu = victoryScene.Instantiate<GameOverMenu>();
					GetTree().CurrentScene.AddChild(menu);
					menu.Setup(true, _clearedRoomsCount, _totalCombatRooms, kills, _player?.Money ?? 0);
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
			GD.Print($"[DungeonManager] Advancing to Floor {FloorNumber}!");

			// Clean up previous rooms & doors
			foreach (var room in _generator.InstantiatedRooms)
			{
				if (GodotObject.IsInstanceValid(room))
				{
					foreach (var door in room.Doors)
					{
						if (GodotObject.IsInstanceValid(door))
							door.QueueFree();
					}
					room.QueueFree();
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
			if (_currentRoom != null && _currentRoom.Data.State == RoomState.Active)
			{
				EnemiesCountChanged?.Invoke(_currentRoom.ActiveEnemies.Count);
			}
		}
	}
}
