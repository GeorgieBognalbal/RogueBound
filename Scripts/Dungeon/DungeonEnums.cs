namespace RogueBound.Dungeon
{
	public enum RoomType
	{
		Spawn,
		Combat,
		Shop,
		Challenge,
		GolemBoss,
		IvernBoss,
		OrcKing,
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
