using Godot;
using System.Collections.Generic;

namespace RogueBound.Dungeon
{
	public class DungeonRoomData
	{
		public Vector2I GridCoord { get; set; }
		public RoomType Type { get; set; }
		public RoomState State { get; set; } = RoomState.Unvisited;
		public Dictionary<DoorDirection, bool> Doors { get; set; } = new()
		{
			{ DoorDirection.North, false },
			{ DoorDirection.South, false },
			{ DoorDirection.East, false },
			{ DoorDirection.West, false }
		};

		public Rect2I TileBounds { get; set; }
		public Rect2 WorldBounds { get; set; }
		public Vector2 WorldCenter => WorldBounds.GetCenter();

		public bool HasDoor(DoorDirection dir) => Doors.ContainsKey(dir) && Doors[dir];
	}
}
