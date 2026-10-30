
using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{

	[Export] public PackedScene UpgradeScene;

	// =========================
	// MOVEMENT & HEALTH
	// =========================

	[Export] public PackedScene DamageNumberScene;
	[Export] public bool Invincible = false;
	[Export] public float Speed = 150.0f;
	[Export] public float MaxHealth = 100.0f;
	[Export] public float DamageInvulnerabilityTime = 0.75f;

	public float health = 100.0f;

	private float _invulnerabilityTimer = 0.0f;
	private bool _godMode = false;

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
			GetNode<AudioStreamPlayer2D>("HitSound").Play();
		}

		if (@event is InputEventKey keyEvent
			&& keyEvent.Pressed
			&& !keyEvent.Echo
			&& keyEvent.Keycode == Key.F1)
		{
			_godMode = !_godMode;
			GD.Print($"GOD MODE: {(_godMode ? "ON" : "OFF")}");
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
		if (_godMode || Invincible)
			return;

		if (damage <= 0 || health <= 0)
			return;

		if (_invulnerabilityTimer > 0)
			return;

		health = Mathf.Max(health - damage, 0);
		_invulnerabilityTimer = DamageInvulnerabilityTime;

		SpawnDamageNumber(damage);

		_animatedSprite.Play("hit");
		
		GetNode<AudioStreamPlayer2D>("PlayerHurt").Play();

		GD.Print($"Player took {damage} damage! HP: {health}/{MaxHealth}");
		HealthChanged?.Invoke(health, MaxHealth);

		if (health <= 0)
		{
		GetNode<AudioStreamPlayer2D>("DeathSound").Play();
		Die();
		}
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

	private void SpawnDamageNumber(float damage)
	{
		if (DamageNumberScene == null)
			return;

		Node currentScene = GetTree().CurrentScene;

		if (currentScene == null)
			return;

		Node2D damageNumber = DamageNumberScene.Instantiate<Node2D>();
		currentScene.AddChild(damageNumber);

		damageNumber.GlobalPosition =
			GlobalPosition + new Vector2(0, -20);

		damageNumber.Call("ShowDamage", damage);
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

	private void ShowUpgradeMenu()
	{
		if (UpgradeScene == null)
		{
			GD.PushError("Assign UpgradeScene in the Inspector!");
			return;
		}
		GetTree().Paused = true;

		Control menu = UpgradeScene.Instantiate<Control>();
		GetTree().Root.AddChild(menu);
	}
}
