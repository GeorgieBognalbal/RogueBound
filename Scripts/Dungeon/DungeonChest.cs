using Godot;

namespace RogueBound.Dungeon
{
	public partial class DungeonChest : Area2D
	{
		[Export] public int GoldAmount = 25;
		[Export] public bool IsOpened { get; private set; } = false;

		private Sprite2D _sprite;
		private AudioStreamPlayer2D _audio;

		public override void _Ready()
		{
			_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
			_audio = GetNodeOrNull<AudioStreamPlayer2D>("OpenAudio");
			BodyEntered += OnBodyEntered;
		}

		private void OnBodyEntered(Node2D body)
		{
			if (IsOpened || !(body is Player player))
				return;

			OpenChest(player);
		}

		public void OpenChest(Player player)
		{
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
			if (GD.Randf() < 0.5f)
			{
				var potScene = GD.Load<PackedScene>("res://Scenes/Items/HealthPickup.tscn");
				if (potScene != null && GetTree().CurrentScene != null)
				{
					Node2D pot = potScene.Instantiate<Node2D>();
					pot.GlobalPosition = GlobalPosition + new Vector2((float)GD.RandRange(-16, 16), (float)GD.RandRange(-16, 16));
					GetTree().CurrentScene.CallDeferred("add_child", pot);
				}
			}

			// Disable collision after opened
			SetDeferred(Area2D.PropertyName.Monitoring, false);
		}
	}
}
