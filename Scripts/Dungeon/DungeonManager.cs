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
		public event Action<float, bool> ChallengeTimerUpdated;

		private DungeonGenerator _generator;
		private TileMapLayer _tileMapLayer;
		private Player _player;
		private DungeonMinimap _minimap;
		private DungeonRoom _currentRoom;

		private int _clearedRoomsCount = 0;
		private int _totalCombatRooms = 0;

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
		}

		private void AttachMinimap()
		{
			var canvasLayer = new CanvasLayer { Name = "MinimapLayer" };
			AddChild(canvasLayer);

			var container = new MarginContainer();
			container.SetAnchorsPreset(Control.LayoutPreset.TopRight);
			container.OffsetLeft = -150;
			container.OffsetTop = 15;
			container.OffsetRight = -15;
			container.OffsetBottom = 150;
			canvasLayer.AddChild(container);

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
			if (room.Data.Type == RoomType.Combat || room.Data.Type == RoomType.BossExit)
			{
				_clearedRoomsCount++;

				// Offer upgrade card selection on every 2 cleared combat rooms
				if (_clearedRoomsCount % 2 == 0 && UpgradeScene != null && GetTree()?.CurrentScene != null)
				{
					var upgrade = UpgradeScene.Instantiate<Control>();
					GetTree().CurrentScene.AddChild(upgrade);
				}
			}

			ProgressChanged?.Invoke(_clearedRoomsCount, _totalCombatRooms);
			EnemiesCountChanged?.Invoke(0);
		}

		private void OnPlayerDied()
		{
			DatabaseManager database = GetNodeOrNull<DatabaseManager>("/root/DatabaseManager");
			database?.FinishRun("Defeated");

			var gameOverScene = GD.Load<PackedScene>("res://Scenes/Menus/GameOverMenu.tscn");
			if (gameOverScene != null && GetTree()?.CurrentScene != null)
			{
				GameOverMenu menu = gameOverScene.Instantiate<GameOverMenu>();
				GetTree().CurrentScene.AddChild(menu);
				menu.Setup(false, _clearedRoomsCount, _totalCombatRooms, 0, _player?.Money ?? 0);
			}
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
