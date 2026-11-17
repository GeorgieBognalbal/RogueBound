using Godot;

namespace RogueBound.Dungeon
{
	public enum ShopConsumable
	{
		None,
		Speed,
		Strength,
		Regeneration,
		Shield
	}

	public static class ConsumableInfo
	{
		public static (string Name, string IconPath, Color Tint) Get(ShopConsumable item) => item switch
		{
			ShopConsumable.Speed => ("Speed Potion", "res://Assets/Combat/speed_potion.svg", Colors.White),
			ShopConsumable.Strength => ("Strength Potion", "res://Assets/Temp/Decals/health pot.png", new Color(1f, 0.72f, 0.35f)),
			ShopConsumable.Regeneration => ("Regen Potion", "res://Assets/Temp/Decals/health pot.png", Colors.White),
			ShopConsumable.Shield => ("Shield", "res://Assets/Combat/shield.svg", Colors.White),
			_ => ("", "", Colors.White)
		};
	}
}
