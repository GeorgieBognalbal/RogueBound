using Godot;
using System;
using RogueBound.Weapons;

namespace RogueBound.Dungeon
{
	/// <summary>
	/// An interactive purchase point in the Shop room.
	/// Can hold either a WeaponData or a potion type.
	/// </summary>
	public partial class ShopPedestal : Area2D
	{
		// ─── Item definition ─────────────────────────────────────────────────
		public WeaponData Weapon    { get; set; }
		public string     PotionType { get; set; }  // "health" | "speed" | null
		public int        Price      { get; set; }

		// ─── State ───────────────────────────────────────────────────────────
		private bool   _sold       = false;
		private bool   _playerNear = false;
		private Player _player;
		private Label  _promptLabel;
		private Sprite2D _itemIcon;

		public override void _Ready()
		{
			BodyEntered += OnBodyEntered;
			BodyExited  += OnBodyExited;

			_promptLabel = GetNodeOrNull<Label>("PromptLabel");
			_itemIcon    = GetNodeOrNull<Sprite2D>("ItemIcon");

			// Populate icon
			if (_itemIcon != null)
			{
				string iconPath = Weapon != null
					? Weapon.IconPath
					: (PotionType == "health"
						? "res://Assets/Temp/Decals/health pot.png"
						: "res://Assets/Temp/Decals/mana pot.png");

				var tex = GD.Load<Texture2D>(iconPath);
				if (tex != null) _itemIcon.Texture = tex;
			}

			// Update price label
			UpdatePrompt(false);
		}

		public override void _Process(double delta)
		{
			if (_sold || !_playerNear) return;

			if (Input.IsActionJustPressed("ui_accept"))
				TryPurchase();
		}

		private void OnBodyEntered(Node2D body)
		{
			if (_sold) return;
			if (body is Player p)
			{
				_player    = p;
				_playerNear = true;
				UpdatePrompt(true);
			}
		}

		private void OnBodyExited(Node2D body)
		{
			if (body is Player)
			{
				_playerNear = false;
				UpdatePrompt(false);
			}
		}

		private void UpdatePrompt(bool visible)
		{
			if (_promptLabel == null) return;

			if (_sold)
			{
				_promptLabel.Text    = "[SOLD]";
				_promptLabel.Visible = true;
				return;
			}

			string itemName = Weapon != null
				? Weapon.Name
				: (PotionType == "health" ? "Health Potion" : "Speed Potion");

			_promptLabel.Text    = visible ? $"[E] Buy {itemName}\n{Price} Gold" : $"{itemName}\n{Price} Gold";
			_promptLabel.Visible = true;
		}

		private void TryPurchase()
		{
			if (_player == null || _sold) return;

			if (!_player.SpendMoney(Price))
			{
				GD.Print("[Shop] Not enough gold!");
				return;
			}

			_sold = true;
			Modulate = new Color(0.4f, 0.4f, 0.4f);

			// Get WeaponComponent on player
			var wc = _player.GetNodeOrNull<WeaponComponent>("WeaponComponent");

			if (Weapon != null)
			{
				if (wc != null)
					wc.Equip(Weapon);
				else
					_player.CollectWeapon(Weapon.Id);

				GD.Print($"[Shop] Bought weapon: {Weapon.Name}");
			}
			else if (PotionType == "health")
			{
				_player.Heal(30.0f);
				GD.Print("[Shop] Used Health Potion (+30 HP)");
			}
			else if (PotionType == "speed")
			{
				// Temporary speed boost applied to Player
				_player.ApplyUpgrade("speed_up");
				GD.Print("[Shop] Used Speed Potion (+20 Speed)");
			}

			UpdatePrompt(false);

			// Play upgrade sound if available
			var sound = GetTree().CurrentScene?.GetNodeOrNull<AudioStreamPlayer>("UpgradeSound");
			sound?.Play();
		}
	}
}
