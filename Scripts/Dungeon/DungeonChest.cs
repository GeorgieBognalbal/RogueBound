using Godot;
using RogueBound.Weapons;

namespace RogueBound.Dungeon
{
	public partial class DungeonChest : Area2D
	{
		[Export] public int GoldAmount = 25;
		[Export] public bool IsOpened { get; private set; } = false;
		[Export] public float WeaponDropChance = 0.35f;

		private Sprite2D _sprite;
		private AudioStreamPlayer2D _audio;
		private bool _roomCleared = true;

		public override void _Ready()
		{
			_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
			_audio = GetNodeOrNull<AudioStreamPlayer2D>("OpenAudio");
			BodyEntered += OnBodyEntered;
		}

		private void OnBodyEntered(Node2D body)
		{
			if (IsOpened || !_roomCleared || !(body is Player player))
				return;

			OpenChest(player);
		}

		public void LockUntilRoomCleared()
		{
			_roomCleared = false;
		}

		public void MarkRoomCleared()
		{
			_roomCleared = true;

			// The player may already be standing on the chest when the last enemy dies.
			foreach (Node2D body in GetOverlappingBodies())
			{
				if (body is Player player)
				{
					OpenChest(player);
					break;
				}
			}
		}

		public void OpenChest(Player player)
		{
			if (IsOpened || !_roomCleared)
				return;

			IsOpened = true;

			// Add gold to player
			player.AddGold(GoldAmount);

			// Sound
			if (_audio != null && _audio.Stream != null)
				_audio.Play();

			// Floating text or visual feedback
			var dmgScene = GD.Load<PackedScene>("res://Scenes/Effects/DamageNumber.tscn");
			if (dmgScene != null && GetTree().CurrentScene != null)
			{
				Node2D number = dmgScene.Instantiate<Node2D>();
				GetTree().CurrentScene.AddChild(number);
				number.GlobalPosition = GlobalPosition + new Vector2(0, -15);
				number.Call("ShowDamage", GoldAmount);
			}

			// Modulate / tint opened chest
			if (_sprite != null)
			{
				_sprite.Modulate = new Color(0.7f, 0.7f, 0.7f, 0.8f);
			}

			// Spawn occasional health pickup
			if (GD.Randf() < 0.6f)
			{
				var potScene = GD.Load<PackedScene>("res://Scenes/Items/HealthPickup.tscn");
				if (potScene != null && GetTree().CurrentScene != null)
				{
					Node2D pot = potScene.Instantiate<Node2D>();
					pot.GlobalPosition = GlobalPosition + new Vector2((float)GD.RandRange(-20, 20), (float)GD.RandRange(-20, 20));
					GetTree().CurrentScene.CallDeferred("add_child", pot);
				}
			}

			// Chance to drop a random weapon
			if (GD.Randf() < WeaponDropChance)
			{
				var wpScene = GD.Load<PackedScene>("res://Scenes/Dungeon/WeaponPickup.tscn");
				if (wpScene != null && GetTree().CurrentScene != null)
				{
					var wp = wpScene.Instantiate<WeaponPickup>();
					wp.Weapon = WeaponData.GetRandomShopWeapon();
					wp.GlobalPosition = GlobalPosition + new Vector2((float)GD.RandRange(-24, 24), 22);
					GetTree().CurrentScene.CallDeferred("add_child", wp);
				}
			}

			// Disable collision after opened
			SetDeferred(Area2D.PropertyName.Monitoring, false);
		}
	}
}
