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
				WeaponData.Staff,
				WeaponData.Dagger
			};
			for (int i = weaponStock.Count - 1; i > 0; i--)
			{
				int swapIndex = (int)GD.RandRange(0, i);
				(weaponStock[i], weaponStock[swapIndex]) = (weaponStock[swapIndex], weaponStock[i]);
			}

			// Build item list: 2 different weapons + 1 health potion + 1 speed potion.
			var items = new List<(WeaponData weapon, string potion, int price)>
			{
				(weaponStock[0], null, 0),
				(weaponStock[1], null, 0),
				(null, "health", 15),
				(null, "speed",  20),
			};

			// Assign prices for weapons
			for (int i = 0; i < items.Count; i++)
			{
				var (w, p, pr) = items[i];
				items[i] = (w, p, w != null ? w.Price : pr);
			}

			// Space pedestals in a horizontal row
			float spacing = 70.0f;
			float startX  = -spacing * (items.Count - 1) / 2.0f;
			float rowY    = roomSize.Y * 0.1f;

			for (int i = 0; i < items.Count; i++)
			{
				var (weapon, potion, price) = items[i];

				var pedestal = PedestalScene.Instantiate<ShopPedestal>();
				pedestal.Position  = new Vector2(startX + i * spacing, rowY);
				pedestal.Weapon    = weapon;
				pedestal.PotionType = potion;
				pedestal.Price     = price;
				AddChild(pedestal);
			}
		}
	}
}
