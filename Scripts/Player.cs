
using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
	// =========================
	// MOVEMENT & HEALTH
	// =========================

	[Export] public float Speed = 150.0f;
	[Export] public float MaxHealth = 100.0f;
	[Export] public float DamageInvulnerabilityTime = 0.75f;

	public float health = 100.0f;

	private float _invulnerabilityTimer = 0.0f;

	// =========================
	// COMBAT
	// =========================

	[Export] public int AttackDamage = 10;

	private AnimatedSprite2D _animatedSprite;
	private bool _isAttacking = false;
	private Area2D _attackArea;

	// =========================
	// MONEY & WEAPONS
	// =========================

	public int Money { get; private set; } = 0;

	public string CurrentWeapon { get; private set; } = "Basic";

	public List<string> OwnedWeapons { get; private set; }
		= new List<string> { "Basic" };

	public event Action<float, float> HealthChanged;
	public event Action<int> MoneyChanged;
	public event Action Died;

	// =========================
	// UPGRADES
	// =========================

	public int DamageUpgradeLevel { get; private set; } = 0;
	public int SpeedUpgradeLevel { get; private set; } = 0;
	public int HealthUpgradeLevel { get; private set; } = 0;

	public override void _Ready()
	{
		_attackArea = GetNode<Area2D>("AttackArea");
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

		health = MaxHealth;

		AddToGroup("player");

		GD.Print($"Player initialized: {health} HP");
	}

	// =========================
	// MOVEMENT & TIMERS
	// =========================

	public override void _PhysicsProcess(double delta)
	{
		if (_invulnerabilityTimer > 0)
			_invulnerabilityTimer -= (float)delta;

		Vector2 direction = Input.GetVector(
			"ui_left",
			"ui_right",
			"ui_up",
            "ui_down"
		);

		if (direction.X != 0)
		{
			bool facingLeft = direction.X < 0;
			_animatedSprite.FlipH = facingLeft;
			_attackArea.Scale = new Vector2(facingLeft ? -1 : 1, 1);
		}

		if (direction != Vector2.Zero)
		{
			Velocity = direction.Normalized() * Speed;

			if (direction.X != 0)
				_animatedSprite.FlipH = direction.X < 0;
		}
		else
		{
			Velocity = Vector2.Zero;
		}

		if (!_isAttacking)
		{
			if (direction != Vector2.Zero)
				_animatedSprite.Play("walk");
			else
				_animatedSprite.Play("idle");
		}

		MoveAndSlide();
	}

	// =========================
	// PLAYER ATTACK
	// =========================

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("attack") && !_isAttacking)
		{
			GD.Print("ATTACK KEY DETECTED!");
			Attack();
		}
	}

	private async void Attack()
	{
		_isAttacking = true;
		_animatedSprite.Play("attack");

		DealDamage();

		await ToSignal(
			_animatedSprite,
			AnimatedSprite2D.SignalName.AnimationFinished
		);

		_isAttacking = false;
	}

	private async void DealDamage()
	{
		await ToSignal(
			GetTree(),
			SceneTree.SignalName.PhysicsFrame
		);

		foreach (Node2D body in _attackArea.GetOverlappingBodies())
		{
			if (body is Enemy enemy)
			{
				GD.Print($"Attacked {enemy.Name}");
				enemy.TakeDamage(AttackDamage, GlobalPosition);
			}
		}
	}

	// =========================
	// PLAYER TAKES DAMAGE
	// =========================

	public void TakeDamage(float damage)
	{
		if (damage <= 0 || health <= 0)
			return;

		// Prevent continuous damage every frame.
		if (_invulnerabilityTimer > 0)
			return;

		health = Mathf.Max(health - damage, 0);
		_invulnerabilityTimer = DamageInvulnerabilityTime;

		_animatedSprite.Play("hit");
		GD.Print($"Player took {damage} damage! HP: {health}/{MaxHealth}");

		HealthChanged?.Invoke(health, MaxHealth);

		if (health <= 0)
			Die();
	}

	public void Heal(float amount)
	{
		if (amount <= 0 || health <= 0)
			return;

		health = Mathf.Min(health + amount, MaxHealth);

		HealthChanged?.Invoke(health, MaxHealth);

		GD.Print($"Player healed! HP: {health}/{MaxHealth}");
	}

	private void Die()
	{
		GD.Print("PLAYER DIED!");

		Velocity = Vector2.Zero;
		SetPhysicsProcess(false);
		SetProcessInput(false);

		Died?.Invoke();

		// TODO: Add death animation, game-over UI,
		// and restart functionality.

		_animatedSprite.Play("death");
	}

	// =========================
	// MONEY COLLECTION
	// =========================

	public void CollectMoney(int amount)
	{
		if (amount <= 0)
			return;

		Money += amount;

		GD.Print($"Collected {amount} coins! Total: {Money}");

		MoneyChanged?.Invoke(Money);
	}

	public bool SpendMoney(int amount)
	{
		if (amount <= 0 || Money < amount)
			return false;

		Money -= amount;

		GD.Print($"Spent {amount} coins. Remaining: {Money}");

		MoneyChanged?.Invoke(Money);

		return true;
	}

	// =========================
	// WEAPON COLLECTION
	// =========================

	public void CollectWeapon(string weaponId)
	{
		if (string.IsNullOrWhiteSpace(weaponId))
			return;

		if (!OwnedWeapons.Contains(weaponId))
		{
			OwnedWeapons.Add(weaponId);

			GD.Print($"Weapon collected: {weaponId}");
		}

		EquipWeapon(weaponId);
	}

	public void EquipWeapon(string weaponId)
	{
		if (!OwnedWeapons.Contains(weaponId))
		{
			GD.PushWarning($"You don't own weapon: {weaponId}");
			return;
		}

		CurrentWeapon = weaponId;

		GD.Print($"Equipped weapon: {CurrentWeapon}");

		// TODO: Connect weaponId to actual weapon behavior,
		// projectiles, attack animations, and weapon stats.
	}

	// =========================
	// UPGRADES
	// =========================

	public void ApplyUpgrade(string upgradeId)
	{
		switch (upgradeId.ToLowerInvariant())
		{
			case "damage_up":
				AttackDamage += 5;
				DamageUpgradeLevel++;

				GD.Print($"Attack damage increased to {AttackDamage}");
				break;

			case "speed_up":
				Speed += 15.0f;
				SpeedUpgradeLevel++;

				GD.Print($"Movement speed increased to {Speed}");
				break;

			case "max_health_up":
				MaxHealth += 20.0f;
				health += 20.0f;
				HealthUpgradeLevel++;

				GD.Print($"Max health increased to {MaxHealth}");

				HealthChanged?.Invoke(health, MaxHealth);
				break;

			case "heal":
				Heal(30.0f);
				break;

			default:
				GD.PushWarning($"Unknown upgrade: {upgradeId}");
				break;
		}
	}
}
