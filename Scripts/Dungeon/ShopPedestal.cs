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
		private string _feedback = "";

		public override void _Ready()
		{
			BodyEntered += OnBodyEntered;
			BodyExited  += OnBodyExited;

			_promptLabel = GetNodeOrNull<Label>("PromptLabel");
			_itemIcon    = GetNodeOrNull<Sprite2D>("ItemIcon");
			if (_promptLabel != null)
			{
				_promptLabel.AddThemeFontSizeOverride("font_size", 10);
				_promptLabel.Visible = false;
			}

			// Populate icon
			if (_itemIcon != null)
			{
				string iconPath = Weapon != null
					? Weapon.IconPath
					: (PotionType == "health"
						? "res://Assets/Temp/Decals/health pot.png"
						: "res://Assets/Temp/Decals/mana pot.png");

				var tex = GD.Load<Texture2D>(iconPath);
				if (tex != null)
				{
					_itemIcon.Texture = tex;
					Vector2 textureSize = tex.GetSize();
					float iconScale = 24.0f / Mathf.Max(textureSize.X, textureSize.Y);
					_itemIcon.Scale = Vector2.One * iconScale;
				}
			}

			// Update price label
			UpdatePrompt(_playerNear);
		}

		public override void _Process(double delta)
		{
			if (_sold || !_playerNear) return;

			if (Input.IsActionJustPressed("interact"))
				TryPurchase();
		}

		private void OnBodyEntered(Node2D body)
		{
			if (body is Player p)
			{
				_player    = p;
				_playerNear = true;
				_feedback = "";
				UpdatePrompt(true);
			}
		}

		private void OnBodyExited(Node2D body)
		{
			if (body is Player)
			{
				_playerNear = false;
				_feedback = "";
				UpdatePrompt(false);
			}
		}

		private void UpdatePrompt(bool visible)
		{
			if (_promptLabel == null) return;
			_promptLabel.Visible = visible;
			if (!visible) return;
			_promptLabel.Modulate = Colors.White;

			if (_sold)
			{
				_promptLabel.Text    = "[SOLD]";
				return;
			}
			if (visible && !string.IsNullOrEmpty(_feedback))
			{
				_promptLabel.Text = _feedback;
				_promptLabel.Modulate = new Color(1.0f, 0.55f, 0.42f);
				return;
			}
			_promptLabel.Modulate = Colors.White;

			string itemName = Weapon != null
				? Weapon.Name
				: (PotionType == "health" ? "Health Potion" : "Speed Potion");

			_promptLabel.Text = $"[E] Buy {itemName}\n{Price} Gold";
		}

		private void TryPurchase()
		{
			if (_player == null || _sold) return;

			if (!_player.SpendMoney(Price))
			{
				_feedback = $"NEED {Mathf.Max(0, Price - _player.Money)} MORE GOLD";
				UpdatePrompt(true);
				GD.Print($"[Shop] Not enough gold for {Weapon?.Name ?? PotionType}: need {Price - _player.Money} more.");
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
				_player.ApplySpeedPotion(20.0f, 10.0f);
				GD.Print("[Shop] Used Speed Potion (+20 Speed for 10 seconds)");
			}

			UpdatePrompt(_playerNear);

			// Play upgrade sound if available
			var sound = GetTree().CurrentScene?.GetNodeOrNull<AudioStreamPlayer>("UpgradeSound");
			sound?.Play();
		}
	}
}
