namespace RogueBound.Dungeon
{
	public enum RoomType
	{
		Spawn,
		Combat,
		Shop,
		Challenge,
		Maze,
		GolemBoss,
		BossExit
	}

	public enum RoomState
	{
		Unvisited,
		Active,
		Cleared
	}

	public enum DoorDirection
	{
		North,
		South,
		East,
		West
	}
}
