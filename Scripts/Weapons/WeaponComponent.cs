using Godot;
using System;
using RogueBound.Weapons;

/// <summary>
/// Manages the player's two weapon slots, attack cooldown, and fires
/// either melee (via Player.Attack area) or ranged (spawns PlayerProjectile).
/// Attach as a child Node of the Player scene.
/// </summary>
public partial class WeaponComponent : Node
{
	// ─── Slots ───────────────────────────────────────────────────────────────
	public WeaponData PrimaryWeapon   { get; private set; }
	public WeaponData SecondaryWeapon { get; private set; }
	public int ActiveSlot             { get; private set; } = 0;

	public WeaponData Current => ActiveSlot == 0 ? PrimaryWeapon : SecondaryWeapon;

	// ─── Signals ─────────────────────────────────────────────────────────────
	public event Action<WeaponData, WeaponData, int> WeaponChanged; // (primary, secondary, activeSlot)

	// ─── Internal ────────────────────────────────────────────────────────────
	private float _cooldownTimer = 0.0f;
	private Player _player;

	[Export] public PackedScene PlayerProjectileScene;

	public override void _Ready()
	{
		_player = GetParent<Player>();

		// Default loadout: Iron Sword in slot 0
		PrimaryWeapon   = WeaponData.Sword;
		SecondaryWeapon = null;

		WeaponChanged?.Invoke(PrimaryWeapon, SecondaryWeapon, ActiveSlot);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_cooldownTimer > 0)
			_cooldownTimer -= (float)delta;
	}

	// ─── Equip ───────────────────────────────────────────────────────────────
	/// <summary>
	/// Equips a weapon into the given slot (0 = primary, 1 = secondary).
	/// Pass slot = -1 to auto-fill the first empty slot (or secondary if both filled).
	/// </summary>
	public void Equip(WeaponData weapon, int slot = -1)
	{
		if (weapon == null) return;

		if (slot == -1)
		{
			// Auto-slot: prefer the slot that is empty
			if (PrimaryWeapon == null)       slot = 0;
			else if (SecondaryWeapon == null) slot = 1;
			else                             slot = 1; // overwrite secondary
		}

		if (slot == 0)      PrimaryWeapon   = weapon;
		else if (slot == 1) SecondaryWeapon = weapon;

		WeaponChanged?.Invoke(PrimaryWeapon, SecondaryWeapon, ActiveSlot);
		GD.Print($"[Weapon] Equipped '{weapon.Name}' in slot {slot}");
	}

	/// <summary>Toggle active slot between 0 and 1.</summary>
	public void SwapWeapon()
	{
		// Can only swap if secondary slot has a weapon
		if (SecondaryWeapon == null) return;

		ActiveSlot = ActiveSlot == 0 ? 1 : 0;
		_cooldownTimer = 0.15f; // small swap penalty
		WeaponChanged?.Invoke(PrimaryWeapon, SecondaryWeapon, ActiveSlot);
		GD.Print($"[Weapon] Swapped to slot {ActiveSlot} → {Current?.Name}");
	}

	// ─── Attack ──────────────────────────────────────────────────────────────
	public bool CanAttack() => _cooldownTimer <= 0 && Current != null;

	/// <summary>
	/// Perform an attack in the given world direction.
	/// </summary>
	public void PerformAttack(Vector2 aimDirection)
	{
		if (!CanAttack()) return;
		var weapon = Current;
		_cooldownTimer = weapon.Cooldown;

		if (weapon.Type == WeaponType.Melee)
		{
			// Let Player's existing melee routine handle animation + area
			_player.DoMeleeAttack(weapon.Damage);
		}
		else
		{
			FireProjectile(aimDirection, weapon);
		}
	}

	private void FireProjectile(Vector2 direction, WeaponData weapon)
	{
		if (PlayerProjectileScene == null)
		{
			PlayerProjectileScene = GD.Load<PackedScene>("res://Scenes/Weapons/PlayerProjectile.tscn");
			if (PlayerProjectileScene == null)
			{
				GD.PushWarning("[Weapon] PlayerProjectile scene not found!");
				return;
			}
		}

		var proj = PlayerProjectileScene.Instantiate<PlayerProjectile>();

		// Load texture from weapon definition
		if (!string.IsNullOrEmpty(weapon.ProjectileTexturePath))
		{
			var tex = GD.Load<Texture2D>(weapon.ProjectileTexturePath);
			if (tex != null) proj.OverrideTexture = tex;
		}

		GetTree().CurrentScene.AddChild(proj);
		proj.GlobalPosition = _player.GlobalPosition;
		proj.Setup(direction, weapon.Damage, weapon.ProjectileSpeed);

		GD.Print($"[Weapon] Fired {weapon.Name} projectile → {direction}");
	}
}
