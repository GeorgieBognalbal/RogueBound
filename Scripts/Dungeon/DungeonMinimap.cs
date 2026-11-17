using Godot;
using System.Collections.Generic;

namespace RogueBound.Dungeon
{
	public partial class DungeonMinimap : Control
	{
		private Dictionary<Vector2I, DungeonRoomData> _rooms = new();
		private Vector2I _currentRoomCoord = new Vector2I(2, 2);
		private HashSet<Vector2I> _discoveredRooms = new();

		[Export] public float CellSize = 16.0f;
		[Export] public float CellGap = 6.0f;
		[Export] public Color BackgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.65f);
		[Export] public Color UnvisitedColor = new Color(0.25f, 0.25f, 0.3f, 0.5f);
		[Export] public Color ClearedColor = new Color(0.5f, 0.55f, 0.6f, 0.9f);
		[Export] public Color CurrentBorderColor = new Color(1.0f, 0.9f, 0.2f, 1.0f);
		[Export] public Color HallwayColor = new Color(0.4f, 0.45f, 0.5f, 0.8f);

		public override void _Ready()
		{
			CustomMinimumSize = new Vector2(130, 130);
		}

		public void Initialize(Dictionary<Vector2I, DungeonRoomData> rooms, Vector2I spawnCoord)
		{
			_rooms = rooms;
			_currentRoomCoord = spawnCoord;
			_discoveredRooms.Clear();
			DiscoverRoom(spawnCoord);
			QueueRedraw();
		}

		public void SetCurrentRoom(Vector2I coord)
		{
			_currentRoomCoord = coord;
			DiscoverRoom(coord);
			QueueRedraw();
		}

		private void DiscoverRoom(Vector2I coord)
		{
			_discoveredRooms.Add(coord);

			// Also reveal adjacent neighbors
			if (_rooms.TryGetValue(coord, out var roomData))
			{
				if (roomData.HasDoor(DoorDirection.North)) _discoveredRooms.Add(coord + new Vector2I(0, -1));
				if (roomData.HasDoor(DoorDirection.South)) _discoveredRooms.Add(coord + new Vector2I(0, 1));
				if (roomData.HasDoor(DoorDirection.East))  _discoveredRooms.Add(coord + new Vector2I(1, 0));
				if (roomData.HasDoor(DoorDirection.West))  _discoveredRooms.Add(coord + new Vector2I(-1, 0));
			}
		}

		public override void _Draw()
		{
			if (_rooms == null || _rooms.Count == 0)
				return;

			// Draw background panel
			DrawRect(new Rect2(Vector2.Zero, Size), BackgroundColor);

			// Find grid bounds
			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;

			foreach (var kvp in _rooms)
			{
				if (!_discoveredRooms.Contains(kvp.Key))
					continue;

				minX = Mathf.Min(minX, kvp.Key.X);
				maxX = Mathf.Max(maxX, kvp.Key.X);
				minY = Mathf.Min(minY, kvp.Key.Y);
				maxY = Mathf.Max(maxY, kvp.Key.Y);
			}

			if (minX > maxX)
				return;

			float stride = CellSize + CellGap;
			float totalW = (maxX - minX + 1) * stride;
			float totalH = (maxY - minY + 1) * stride;

			Vector2 origin = (Size - new Vector2(totalW, totalH)) * 0.5f;

			// Draw hallways first
			foreach (var kvp in _rooms)
			{
				if (!_discoveredRooms.Contains(kvp.Key))
					continue;

				Vector2 posA = origin + new Vector2(
					(kvp.Key.X - minX) * stride + CellSize * 0.5f,
					(kvp.Key.Y - minY) * stride + CellSize * 0.5f
				);

				var data = kvp.Value;
				if (data.HasDoor(DoorDirection.East))
				{
					Vector2I eastCoord = kvp.Key + new Vector2I(1, 0);
					if (_discoveredRooms.Contains(eastCoord))
					{
						Vector2 posB = origin + new Vector2(
							(eastCoord.X - minX) * stride + CellSize * 0.5f,
							(eastCoord.Y - minY) * stride + CellSize * 0.5f
						);
						DrawLine(posA, posB, HallwayColor, 2.0f);
					}
				}

				if (data.HasDoor(DoorDirection.South))
				{
					Vector2I southCoord = kvp.Key + new Vector2I(0, 1);
					if (_discoveredRooms.Contains(southCoord))
					{
						Vector2 posB = origin + new Vector2(
							(southCoord.X - minX) * stride + CellSize * 0.5f,
							(southCoord.Y - minY) * stride + CellSize * 0.5f
						);
						DrawLine(posA, posB, HallwayColor, 2.0f);
					}
				}
			}

			// Draw room boxes
			foreach (var kvp in _rooms)
			{
				if (!_discoveredRooms.Contains(kvp.Key))
					continue;

				var data = kvp.Value;
				Vector2 roomTopLeft = origin + new Vector2(
					(kvp.Key.X - minX) * stride,
					(kvp.Key.Y - minY) * stride
				);

				Rect2 roomRect = new Rect2(roomTopLeft, new Vector2(CellSize, CellSize));

				Color fill = data.State == RoomState.Cleared ? ClearedColor : UnvisitedColor;

				// Special room colors
				if (data.Type == RoomType.Shop)
					fill = new Color(0.85f, 0.7f, 0.2f, 0.85f);
				else if (data.Type == RoomType.Challenge)
					fill = new Color(0.75f, 0.25f, 0.75f, 0.85f);
				else if (data.Type == RoomType.BossExit)
					fill = new Color(0.2f, 0.7f, 0.85f, 0.85f);
				else if (data.Type == RoomType.GolemBoss)
					fill = new Color(0.8f, 0.25f, 0.18f, 0.9f);
				else if (data.Type == RoomType.Spawn)
					fill = new Color(0.3f, 0.75f, 0.35f, 0.85f);

				DrawRect(roomRect, fill);

				// Highlight current room
				if (kvp.Key == _currentRoomCoord)
				{
					DrawRect(roomRect, CurrentBorderColor, false, 2.0f);
				}
			}
		}
	}
}
