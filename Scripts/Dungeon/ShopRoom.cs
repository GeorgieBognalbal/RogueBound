using Godot;
using System.Collections.Generic;
using RogueBound.Weapons;

namespace RogueBound.Dungeon
{
	/// <summary>
	/// Populates a Shop room with a shopkeeper sprite and several ShopPedestal nodes.
	/// Called by DungeonRoom when it detects RoomType.Shop.
	/// </summary>
	public partial class ShopRoom : Node2D
	{
		private static readonly PackedScene PedestalScene =
			GD.Load<PackedScene>("res://Scenes/Dungeon/ShopPedestal.tscn");

		/// <summary>
		/// Build the shop layout inside the given room bounds.
		/// </summary>
		public void Setup(Vector2 roomSize)
		{
			SpawnShopkeeper(roomSize);
			SpawnPedestals(roomSize);
		}

		private void SpawnShopkeeper(Vector2 roomSize)
		{
			var texture = GD.Load<Texture2D>("res://Assets/Shop/Large sized/shop_cart_sized.png");
			if (texture == null)
			{
				GD.PushWarning("[ShopRoom] Could not load the market stall sprite.");
				return;
			}

			var sprite = new Sprite2D
			{
				Name = "MarketStall",
				Texture = texture,
				Position = new Vector2(0, -roomSize.Y * 0.25f),
				Offset = new Vector2(144, -104),
				Scale = new Vector2(0.075f, 0.075f),
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest
			};
			AddChild(sprite);
		}

		private void SpawnPedestals(Vector2 roomSize)
		{
			if (PedestalScene == null)
			{
				GD.PushWarning("[ShopRoom] ShopPedestal.tscn not found!");
				return;
			}

			// Shuffle the weapon pool and take two entries so a shop never offers duplicates.
			var weaponStock = new List<WeaponData>
			{
				WeaponData.Bow,
				WeaponData.Battleaxe,
				WeaponData.Dagger
			};
			for (int i = weaponStock.Count - 1; i > 0; i--)
			{
				int swapIndex = (int)GD.RandRange(0, i);
				(weaponStock[i], weaponStock[swapIndex]) = (weaponStock[swapIndex], weaponStock[i]);
			}
			// A staff replaces one regular offer in 15% of shops.
			if (GD.Randf() < 0.15f)
				weaponStock[1] = WeaponData.Staff;

			var items = new (WeaponData weapon, ShopConsumable consumable, int price)[]
			{
				(weaponStock[0], ShopConsumable.None, weaponStock[0].Price),
				(weaponStock[1], ShopConsumable.None, weaponStock[1].Price),
				(null, ShopConsumable.Shield, 25),
				(null, ShopConsumable.Regeneration, 15),
				(null, ShopConsumable.Speed, 20),
				(null, ShopConsumable.Strength, 25),
			};

			float spacing = Mathf.Min(76f, roomSize.X / 4f);
			for (int i = 0; i < items.Length; i++)
			{
				var (weapon, consumable, price) = items[i];

				var pedestal = PedestalScene.Instantiate<ShopPedestal>();
				pedestal.Position  = new Vector2((i % 3 - 1) * spacing, -12f + i / 3 * 72f);
				pedestal.Weapon    = weapon;
				pedestal.Consumable = consumable;
				pedestal.Price     = price;
				AddChild(pedestal);
			}
		}
	}
}
