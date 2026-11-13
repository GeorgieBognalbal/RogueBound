using Godot;
using System;
using System.Collections.Generic;

namespace RogueBound.Dungeon
{
	public partial class DungeonGenerator : Node
	{
		[Export] public int GridWidth = 5;
		[Export] public int GridHeight = 5;
		[Export] public int TargetRoomCount = 8;

		[Export] public int RoomTileWidth = 22;
		[Export] public int RoomTileHeight = 16;
		[Export] public int StrideTileX = 32;
		[Export] public int StrideTileY = 24;
		[Export] public int TilePixelSize = 16;

		[Export] public int FloorSourceId = 1;
		[Export] public Vector2I FloorAtlasCoord = new Vector2I(0, 0);

		[Export] public int WallSourceId = 2;
		[Export] public Vector2I WallAtlasCoord = new Vector2I(0, 0);

		public Dictionary<Vector2I, DungeonRoomData> RoomMap { get; private set; } = new();
		public List<DungeonRoom> InstantiatedRooms { get; private set; } = new();
		public DungeonRoomData SpawnRoomData { get; private set; }

		private static readonly Dictionary<DoorDirection, Vector2I> DirectionOffsets = new()
		{
			{ DoorDirection.North, new Vector2I(0, -1) },
			{ DoorDirection.South, new Vector2I(0, 1) },
			{ DoorDirection.East,  new Vector2I(1, 0) },
			{ DoorDirection.West,  new Vector2I(-1, 0) }
		};

		private static readonly Dictionary<DoorDirection, DoorDirection> OppositeDirections = new()
		{
			{ DoorDirection.North, DoorDirection.South },
			{ DoorDirection.South, DoorDirection.North },
			{ DoorDirection.East,  DoorDirection.West },
			{ DoorDirection.West,  DoorDirection.East }
		};

		public bool GenerateLayout()
		{
			RoomMap.Clear();

			Vector2I startPos = new Vector2I(GridWidth / 2, GridHeight / 2);
			var spawnRoom = new DungeonRoomData
			{
				GridCoord = startPos,
				Type = RoomType.Spawn,
				State = RoomState.Cleared
			};
			RoomMap[startPos] = spawnRoom;
			SpawnRoomData = spawnRoom;

			var frontier = new List<Vector2I> { startPos };
			var rng = new Random();

			while (RoomMap.Count < TargetRoomCount && frontier.Count > 0)
			{
				int currentIdx = rng.Next(frontier.Count);
				Vector2I currentPos = frontier[currentIdx];

				var directions = new List<DoorDirection>
				{
					DoorDirection.North, DoorDirection.South,
					DoorDirection.East,  DoorDirection.West
				};

				// Shuffle directions
				for (int i = directions.Count - 1; i > 0; i--)
				{
					int k = rng.Next(i + 1);
					(directions[i], directions[k]) = (directions[k], directions[i]);
				}

				bool expanded = false;
				foreach (var dir in directions)
				{
					Vector2I nextPos = currentPos + DirectionOffsets[dir];

					if (nextPos.X < 0 || nextPos.X >= GridWidth ||
						nextPos.Y < 0 || nextPos.Y >= GridHeight)
						continue;

					if (!RoomMap.ContainsKey(nextPos))
					{
						var newRoom = new DungeonRoomData
						{
							GridCoord = nextPos,
							Type = RoomType.Combat
						};

						// Connect doors
						RoomMap[currentPos].Doors[dir] = true;
						newRoom.Doors[OppositeDirections[dir]] = true;

						RoomMap[nextPos] = newRoom;
						frontier.Add(nextPos);
						expanded = true;

						if (RoomMap.Count >= TargetRoomCount)
							break;
					}
					else
					{
						// Chance to create a loop/shortcut between adjacent rooms
						if (rng.NextDouble() < 0.25)
						{
							RoomMap[currentPos].Doors[dir] = true;
							RoomMap[nextPos].Doors[OppositeDirections[dir]] = true;
						}
					}
				}

				if (!expanded)
				{
					frontier.RemoveAt(currentIdx);
				}
			}

			AssignSpecialRooms();
			CalculateBounds();
			return true;
		}

		private void AssignSpecialRooms()
		{
			// Find room furthest from spawn
			Vector2I spawnPos = SpawnRoomData.GridCoord;
			Vector2I furthestPos = spawnPos;
			int maxDist = -1;

			var candidates = new List<Vector2I>();

			foreach (var kvp in RoomMap)
			{
				if (kvp.Key == spawnPos)
					continue;

				int dist = Math.Abs(kvp.Key.X - spawnPos.X) + Math.Abs(kvp.Key.Y - spawnPos.Y);
				if (dist > maxDist)
				{
					maxDist = dist;
					furthestPos = kvp.Key;
				}

				candidates.Add(kvp.Key);
			}

			// Furthest becomes BossExit
			RoomMap[furthestPos].Type = RoomType.BossExit;
			candidates.Remove(furthestPos);

			// Assign 1 Shop
			if (candidates.Count > 0)
			{
				RoomMap[candidates[0]].Type = RoomType.Shop;
				candidates.RemoveAt(0);
			}

			// Assign 1 Challenge
			if (candidates.Count > 0)
			{
				RoomMap[candidates[0]].Type = RoomType.Challenge;
				candidates.RemoveAt(0);
			}

			// Assign 1 Maze
			if (candidates.Count > 0)
			{
				RoomMap[candidates[0]].Type = RoomType.Maze;
				candidates.RemoveAt(0);
			}

			// All remaining candidates stay RoomType.Combat
		}

		private void CalculateBounds()
		{
			foreach (var kvp in RoomMap)
			{
				var room = kvp.Value;
				int tx = room.GridCoord.X * StrideTileX;
				int ty = room.GridCoord.Y * StrideTileY;

				room.TileBounds = new Rect2I(tx, ty, RoomTileWidth, RoomTileHeight);
				room.WorldBounds = new Rect2(
					tx * TilePixelSize,
					ty * TilePixelSize,
					RoomTileWidth * TilePixelSize,
					RoomTileHeight * TilePixelSize
				);
			}
		}

		public void BuildDungeonTiles(TileMapLayer layer, Node dungeonRoot)
		{
			if (layer == null)
			{
				GD.PushError("DungeonGenerator: TileMapLayer is null!");
				return;
			}

			layer.Clear();
			InstantiatedRooms.Clear();

			var doorScene = GD.Load<PackedScene>("res://Scenes/Dungeon/Door.tscn");

			// 1. Carve rooms
			foreach (var kvp in RoomMap)
			{
				var roomData = kvp.Value;
				int startX = roomData.TileBounds.Position.X;
				int startY = roomData.TileBounds.Position.Y;
				int w = roomData.TileBounds.Size.X;
				int h = roomData.TileBounds.Size.Y;

				// Fill floor
				for (int x = startX + 1; x < startX + w - 1; x++)
				{
					for (int y = startY + 1; y < startY + h - 1; y++)
					{
						layer.SetCell(new Vector2I(x, y), FloorSourceId, FloorAtlasCoord);
					}
				}

				// If Maze room, carve labyrinth partitions
				if (roomData.Type == RoomType.Maze)
				{
					for (int x = startX + 3; x < startX + w - 5; x++)
					{
						layer.SetCell(new Vector2I(x, startY + 5), WallSourceId, WallAtlasCoord);
					}

					for (int x = startX + 5; x < startX + w - 3; x++)
					{
						layer.SetCell(new Vector2I(x, startY + 10), WallSourceId, WallAtlasCoord);
					}
				}
				// If Challenge room, place obstacle pillars for cover
				else if (roomData.Type == RoomType.Challenge)
				{
					int[] ox = new[] { startX + 5, startX + w - 6 };
					int[] oy = new[] { startY + 4, startY + h - 5 };
					foreach (int px in ox)
					{
						foreach (int py in oy)
						{
							layer.SetCell(new Vector2I(px, py), WallSourceId, WallAtlasCoord);
						}
					}
				}

				// Perimeter walls
				for (int x = startX; x < startX + w; x++)
				{
					layer.SetCell(new Vector2I(x, startY), WallSourceId, WallAtlasCoord);
					layer.SetCell(new Vector2I(x, startY + h - 1), WallSourceId, WallAtlasCoord);
				}
				for (int y = startY; y < startY + h; y++)
				{
					layer.SetCell(new Vector2I(startX, y), WallSourceId, WallAtlasCoord);
					layer.SetCell(new Vector2I(startX + w - 1, y), WallSourceId, WallAtlasCoord);
				}

				// Create DungeonRoom node
				var roomNode = new DungeonRoom();
				roomNode.Name = $"Room_{roomData.GridCoord.X}_{roomData.GridCoord.Y}";
				dungeonRoot.AddChild(roomNode);
				roomNode.Initialize(roomData);
				InstantiatedRooms.Add(roomNode);

				// Door openings in walls
				int midX = startX + w / 2;
				int midY = startY + h / 2;

				if (roomData.HasDoor(DoorDirection.North))
				{
					for (int dx = -1; dx <= 1; dx++)
						layer.SetCell(new Vector2I(midX + dx, startY), FloorSourceId, FloorAtlasCoord);

					if (doorScene != null)
					{
						var door = doorScene.Instantiate<DungeonDoor>();
						door.Direction = DoorDirection.North;
						dungeonRoot.AddChild(door);
						door.GlobalPosition = new Vector2(midX * TilePixelSize + 8, startY * TilePixelSize + 8);
						roomNode.Doors.Add(door);
					}
				}

				if (roomData.HasDoor(DoorDirection.South))
				{
					for (int dx = -1; dx <= 1; dx++)
						layer.SetCell(new Vector2I(midX + dx, startY + h - 1), FloorSourceId, FloorAtlasCoord);

					if (doorScene != null)
					{
						var door = doorScene.Instantiate<DungeonDoor>();
						door.Direction = DoorDirection.South;
						dungeonRoot.AddChild(door);
						door.GlobalPosition = new Vector2(midX * TilePixelSize + 8, (startY + h - 1) * TilePixelSize + 8);
						roomNode.Doors.Add(door);
					}
				}

				if (roomData.HasDoor(DoorDirection.East))
				{
					for (int dy = -1; dy <= 1; dy++)
						layer.SetCell(new Vector2I(startX + w - 1, midY + dy), FloorSourceId, FloorAtlasCoord);

					if (doorScene != null)
					{
						var door = doorScene.Instantiate<DungeonDoor>();
						door.Direction = DoorDirection.East;
						dungeonRoot.AddChild(door);
						door.GlobalPosition = new Vector2((startX + w - 1) * TilePixelSize + 8, midY * TilePixelSize + 8);
						roomNode.Doors.Add(door);
					}
				}

				if (roomData.HasDoor(DoorDirection.West))
				{
					for (int dy = -1; dy <= 1; dy++)
						layer.SetCell(new Vector2I(startX, midY + dy), FloorSourceId, FloorAtlasCoord);

					if (doorScene != null)
					{
						var door = doorScene.Instantiate<DungeonDoor>();
						door.Direction = DoorDirection.West;
						dungeonRoot.AddChild(door);
						door.GlobalPosition = new Vector2(startX * TilePixelSize + 8, midY * TilePixelSize + 8);
						roomNode.Doors.Add(door);
					}
				}
			}

			// 2. Carve connecting corridors
			foreach (var kvp in RoomMap)
			{
				var room = kvp.Value;
				int midX = room.TileBounds.Position.X + room.TileBounds.Size.X / 2;
				int midY = room.TileBounds.Position.Y + room.TileBounds.Size.Y / 2;

				if (room.HasDoor(DoorDirection.East))
				{
					Vector2I eastCoord = room.GridCoord + DirectionOffsets[DoorDirection.East];
					if (RoomMap.TryGetValue(eastCoord, out var neighbor))
					{
						int startCorridorX = room.TileBounds.Position.X + room.TileBounds.Size.X;
						int endCorridorX = neighbor.TileBounds.Position.X;
						int corridorMidY = midY;

						for (int x = startCorridorX; x < endCorridorX; x++)
						{
							// Walls on top and bottom of hallway
							layer.SetCell(new Vector2I(x, corridorMidY - 2), WallSourceId, WallAtlasCoord);
							layer.SetCell(new Vector2I(x, corridorMidY + 2), WallSourceId, WallAtlasCoord);

							// 3 tiles floor
							for (int dy = -1; dy <= 1; dy++)
							{
								layer.SetCell(new Vector2I(x, corridorMidY + dy), FloorSourceId, FloorAtlasCoord);
							}
						}
					}
				}

				if (room.HasDoor(DoorDirection.South))
				{
					Vector2I southCoord = room.GridCoord + DirectionOffsets[DoorDirection.South];
					if (RoomMap.TryGetValue(southCoord, out var neighbor))
					{
						int startCorridorY = room.TileBounds.Position.Y + room.TileBounds.Size.Y;
						int endCorridorY = neighbor.TileBounds.Position.Y;
						int corridorMidX = midX;

						for (int y = startCorridorY; y < endCorridorY; y++)
						{
							// Walls on left and right of hallway
							layer.SetCell(new Vector2I(corridorMidX - 2, y), WallSourceId, WallAtlasCoord);
							layer.SetCell(new Vector2I(corridorMidX + 2, y), WallSourceId, WallAtlasCoord);

							// 3 tiles floor
							for (int dx = -1; dx <= 1; dx++)
							{
								layer.SetCell(new Vector2I(corridorMidX + dx, y), FloorSourceId, FloorAtlasCoord);
							}
						}
					}
				}
			}
		}
	}
}
