using Godot;

namespace RogueBound.Dungeon
{
	public partial class DungeonDoor : StaticBody2D
	{
		[Export] public DoorDirection Direction = DoorDirection.North;
		[Export] public bool IsOpen { get; private set; } = true;

		private Sprite2D _sprite;
		private CollisionShape2D _collision;
		private AudioStreamPlayer2D _audio;

		private Texture2D _texClosedH;
		private Texture2D _texOpenH;
		private Texture2D _texClosedV;
		private Texture2D _texOpenV;

		public override void _Ready()
		{
			_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
			_collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			_audio = GetNodeOrNull<AudioStreamPlayer2D>("DoorAudio");

			CollisionLayer = 1; // Wall / obstacle
			CollisionMask = 3;  // Player and enemies

			_texClosedH = GD.Load<Texture2D>("res://Decals/dungeon_door_closed_h.png");
			_texOpenH = GD.Load<Texture2D>("res://Decals/dungeon_door_open_h.png");
			_texClosedV = GD.Load<Texture2D>("res://Decals/dungeon_door_closed_v.png");
			_texOpenV = GD.Load<Texture2D>("res://Decals/dungeon_door_open_v.png");

			if (_collision != null)
			{
				_collision.Disabled = IsOpen;
			}

			UpdateVisuals();
		}

		public void Open()
		{
			if (IsOpen)
				return;

			IsOpen = true;
			if (_collision != null)
				_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

			UpdateVisuals();

			if (_audio != null && _audio.Stream != null)
				_audio.Play();
		}

		public void Close()
		{
			if (!IsOpen)
				return;

			IsOpen = false;
			if (_collision != null)
				_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);

			UpdateVisuals();

			if (_audio != null && _audio.Stream != null)
				_audio.Play();
		}

		private void UpdateVisuals()
		{
			if (_sprite == null)
				return;

			bool isHorizontal = Direction == DoorDirection.North || Direction == DoorDirection.South;

			if (isHorizontal)
			{
				_sprite.Texture = IsOpen ? _texOpenH : _texClosedH;
				if (_collision?.Shape is RectangleShape2D rect)
				{
					rect.Size = new Vector2(48, 16);
				}
			}
			else
			{
				_sprite.Texture = IsOpen ? _texOpenV : _texClosedV;
				if (_collision?.Shape is RectangleShape2D rect)
				{
					rect.Size = new Vector2(16, 48);
				}
			}
		}
	}
}
