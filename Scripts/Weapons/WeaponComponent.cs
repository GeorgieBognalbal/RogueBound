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
	private float _dashCooldownTimer = 0.0f;
	public float DashCooldownRemaining => Mathf.Max(0.0f, _dashCooldownTimer);
	private Player _player;
	private bool _isBowCharging = false;
	private float _bowChargeTime = 0.0f;
	private bool _isStaffCharging = false;
	private float _staffChargeTime = 0.0f;
	[Export] public float BowMaxChargeSeconds = 1.5f;
	[Export] public float BowMaxSpeedMultiplier = 2.5f;
	[Export] public float StaffMaxChargeSeconds = 2.5f;
	[Export] public float StaffMinChargeRadius = 45.0f;
	[Export] public float StaffChargeRadius = 115.0f;
	[Export] public float StaffMaxDamageMultiplier = 2.0f;
	private ProgressBar _chargeBar;

	[Export] public PackedScene PlayerProjectileScene;

	public override void _Ready()
	{
		_player = GetParent<Player>();

		_chargeBar = new ProgressBar
		{
			Name = "WeaponChargeBar",
			Position = new Vector2(-16.0f, -30.0f),
			Size = new Vector2(32.0f, 4.0f),
			MinValue = 0.0f,
			MaxValue = 1.0f,
			ShowPercentage = false,
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_player.AddChild(_chargeBar);

		// Default loadout: Iron Sword in slot 0
		PrimaryWeapon   = WeaponData.Sword;
		SecondaryWeapon = null;

		WeaponChanged?.Invoke(PrimaryWeapon, SecondaryWeapon, ActiveSlot);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_cooldownTimer > 0)
			_cooldownTimer -= (float)delta;

		if (_dashCooldownTimer > 0.0f)
			_dashCooldownTimer = Mathf.Max(0.0f, _dashCooldownTimer - (float)delta);

		if (_isBowCharging)
		{
			float maxCharge = Mathf.Max(0.01f, BowMaxChargeSeconds);
			_bowChargeTime = Mathf.Min(maxCharge, _bowChargeTime + (float)delta);
			if (_chargeBar != null)
				_chargeBar.Value = _bowChargeTime / maxCharge;
		}

		if (_isStaffCharging)
		{
			float maxCharge = Mathf.Max(0.01f, StaffMaxChargeSeconds);
			_staffChargeTime = Mathf.Min(maxCharge, _staffChargeTime + (float)delta);
			if (_chargeBar != null)
				_chargeBar.Value = _staffChargeTime / maxCharge;
		}
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
	public void BeginAttack(Vector2 aimDirection)
	{
		if (!CanAttack()) return;
		var weapon = Current;
		if (weapon.Id == "bow")
		{
			_isBowCharging = true;
			_bowChargeTime = 0.0f;
			ShowChargeBar();
			return;
		}

		if (weapon.Id == "staff")
		{
			_isStaffCharging = true;
			_staffChargeTime = 0.0f;
			ShowChargeBar();
			return;
		}

		_cooldownTimer = weapon.Cooldown;
		if (weapon.Type == WeaponType.Melee)
		{
			if (weapon.AttackDashDistance > 0.0f && _dashCooldownTimer <= 0.0f)
			{
				_player.PerformWeaponDash(aimDirection, weapon.AttackDashDistance, weapon.AttackDashDuration);
				_dashCooldownTimer = Mathf.Max(0.0f, weapon.AttackDashCooldown);
			}
			_player.DoMeleeAttack(weapon.Damage);
		}
		else
			FireProjectile(aimDirection, weapon, weapon.ProjectileSpeed);
	}

	public void ReleaseAttack(Vector2 aimDirection)
	{
		if (_isStaffCharging)
		{
			_isStaffCharging = false;
			HideChargeBar();

			var staff = Current;
			if (staff == null || staff.Id != "staff")
				return;

			float staffChargeRatio = Mathf.Clamp(
				_staffChargeTime / Mathf.Max(0.01f, StaffMaxChargeSeconds), 0.0f, 1.0f);
			float damage = staff.Damage * Mathf.Lerp(1.0f, StaffMaxDamageMultiplier, staffChargeRatio);
			float radius = Mathf.Lerp(StaffMinChargeRadius, StaffChargeRadius, staffChargeRatio);
			_cooldownTimer = staff.Cooldown;
			DischargeStaff(damage, radius);
			return;
		}

		if (!_isBowCharging) return;

		_isBowCharging = false;
		HideChargeBar();

		var weapon = Current;
		if (weapon == null || weapon.Id != "bow")
			return;

		float bowChargeRatio = Mathf.Clamp(_bowChargeTime / Mathf.Max(0.01f, BowMaxChargeSeconds), 0.0f, 1.0f);
		float speedMultiplier = Mathf.Lerp(1.0f, BowMaxSpeedMultiplier, bowChargeRatio);
		_cooldownTimer = weapon.Cooldown;
		FireProjectile(aimDirection, weapon, weapon.ProjectileSpeed * speedMultiplier);
	}

	private void ShowChargeBar()
	{
		if (_chargeBar == null) return;
		_chargeBar.Value = 0.0f;
		_chargeBar.Visible = true;
	}

	private void HideChargeBar()
	{
		if (_chargeBar != null)
			_chargeBar.Visible = false;
	}

	private void DischargeStaff(float damage, float radius)
	{
		var lightning = new LightningBurst { Radius = radius };
		GetTree().CurrentScene.AddChild(lightning);
		lightning.GlobalPosition = _player.GlobalPosition;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is Enemy enemy
				&& !enemy.IsDead()
				&& enemy.GlobalPosition.DistanceTo(_player.GlobalPosition) <= radius)
			{
				enemy.TakeDamage(damage, _player.GlobalPosition);
			}
		}
	}

	private void FireProjectile(Vector2 direction, WeaponData weapon, float projectileSpeed)
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
		proj.Setup(direction, weapon.Damage, projectileSpeed);

		GD.Print($"[Weapon] Fired {weapon.Name} projectile → {direction}");
	}
}
