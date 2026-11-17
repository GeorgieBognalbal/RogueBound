using Godot;
using System;
using System.Collections.Generic;

namespace RogueBound.Weapons
{
	public enum WeaponType
	{
		Melee,
		Ranged
	}

	public class WeaponData
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public WeaponType Type { get; set; }
		public int Damage { get; set; }
		public float Cooldown { get; set; }
		public float Knockback { get; set; }
		public string IconPath { get; set; }
		public string ProjectileTexturePath { get; set; }
		public float ProjectileSpeed { get; set; }
		public float AttackDashDistance { get; set; }
		public float AttackDashDuration { get; set; }
		public float AttackDashCooldown { get; set; }
		public int Price { get; set; }

		public static WeaponData Sword => new WeaponData
		{
			Id = "sword",
			Name = "Iron Sword",
			Type = WeaponType.Melee,
			Damage = 12,
			Cooldown = 0.35f,
			Knockback = 180.0f,
			IconPath = "res://Decals/weapon_sword.png",
			Price = 10
		};

		public static WeaponData Bow => new WeaponData
		{
			Id = "bow",
			Name = "Hunter's Bow",
			Type = WeaponType.Ranged,
			Damage = 15,
			Cooldown = 0.45f,
			Knockback = 120.0f,
			IconPath = "res://Decals/weapon_bow.png",
			ProjectileTexturePath = "res://Assets/arrow.png",
			ProjectileSpeed = 420.0f,
			Price = 25
		};

		public static WeaponData Battleaxe => new WeaponData
		{
			Id = "battleaxe",
			Name = "Heavy Battleaxe",
			Type = WeaponType.Melee,
			Damage = 26,
			Cooldown = 0.70f,
			Knockback = 350.0f,
			IconPath = "res://Decals/weapon_battleaxe.png",
			Price = 45
		};

		public static WeaponData Staff => new WeaponData
		{
			Id = "staff",
			Name = "Arcane Staff",
			Type = WeaponType.Ranged,
			Damage = 14,
			Cooldown = 0.30f,
			Knockback = 90.0f,
			IconPath = "res://Decals/weapon_staff.png",
			ProjectileTexturePath = "res://Decals/magic_bolt.png",
			ProjectileSpeed = 380.0f,
			Price = 150
		};

		public static WeaponData Dagger => new WeaponData
		{
			Id = "dagger",
			Name = "Shadow Dagger",
			Type = WeaponType.Melee,
			Damage = 7,
			Cooldown = 0.18f,
			Knockback = 80.0f,
			IconPath = "res://Decals/weapon_dagger.png",
			AttackDashDistance = 96.0f,
			AttackDashDuration = 0.16f,
			AttackDashCooldown = 2.0f,
			Price = 30
		};

		public static readonly List<WeaponData> AllWeapons = new()
		{
			Sword, Bow, Battleaxe, Staff, Dagger
		};

		public static WeaponData GetById(string id)
		{
			foreach (var w in AllWeapons)
			{
				if (w.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
					return w;
			}
			return Sword;
		}

		public static WeaponData GetRandomShopWeapon()
		{
			var pool = new[] { Bow, Battleaxe, Staff, Dagger };
			int idx = (int)GD.RandRange(0, pool.Length - 1);
			return pool[idx];
		}

		public static WeaponData GetRandomChestWeapon()
		{
			var pool = new[] { Bow, Battleaxe, Dagger };
			return pool[GD.RandRange(0, pool.Length - 1)];
		}
	}
}
