using Godot;
using System;
using RogueBound.Weapons;

namespace RogueBound.Dungeon
{
	/// <summary>
	/// Represents an interactable weapon drop spawned on the ground (e.g. from chests).
	/// </summary>
	public partial class WeaponPickup : Area2D
	{
		public WeaponData Weapon { get; set; }

		private Sprite2D _iconSprite;
		private Label _label;
		private float _bobTimer = 0.0f;
		private Vector2 _initialPos;
		private bool _playerNear = false;
		private Player _player;

		public override void _Ready()
		{
			_initialPos = Position;
			_iconSprite = GetNodeOrNull<Sprite2D>("Sprite2D");
			_label = GetNodeOrNull<Label>("Label");

			if (Weapon == null)
			{
				Weapon = WeaponData.GetRandomShopWeapon();
			}

			if (_iconSprite != null && !string.IsNullOrEmpty(Weapon.IconPath))
			{
				var tex = GD.Load<Texture2D>(Weapon.IconPath);
				if (tex != null) _iconSprite.Texture = tex;
			}

			if (_label != null)
			{
				_label.Text = Weapon.Name;
			}

			BodyEntered += OnBodyEntered;
			BodyExited += OnBodyExited;
		}

		public override void _PhysicsProcess(double delta)
		{
			_bobTimer += (float)delta * 3.5f;
			Position = new Vector2(_initialPos.X, _initialPos.Y + Mathf.Sin(_bobTimer) * 3.0f);

			if (_playerNear && Input.IsActionJustPressed("ui_accept"))
			{
				CollectWeapon();
			}
		}

		private void OnBodyEntered(Node2D body)
		{
			if (body is Player player)
			{
				_player = player;
				_playerNear = true;
				if (_label != null) _label.Text = $"[E] Take {Weapon.Name}";
			}
		}

		private void OnBodyExited(Node2D body)
		{
			if (body is Player)
			{
				_playerNear = false;
				if (_label != null) _label.Text = Weapon.Name;
			}
		}

		private void CollectWeapon()
		{
			if (_player == null) return;

			var wc = _player.GetNodeOrNull<WeaponComponent>("WeaponComponent");
			if (wc != null)
			{
				wc.Equip(Weapon);
			}
			else
			{
				_player.CollectWeapon(Weapon.Id);
			}

			GD.Print($"[WeaponPickup] Player picked up: {Weapon.Name}");
			QueueFree();
		}
	}
}
