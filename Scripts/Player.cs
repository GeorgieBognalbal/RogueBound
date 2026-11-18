
using Godot;
using System;
using System.Collections.Generic;
using RogueBound.Weapons;
using RogueBound.Dungeon;

public partial class Player : CharacterBody2D
{
	[Export] public PackedScene HUDScene;
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
	public bool DebugGodMode { get => _godMode; set => _godMode = value; }
	private bool _bossIntroLocked;
	private bool _bossIntroWasInvincible;
	private bool _rooted;
	private float _rootTimer;
	private float _stunTimer;
	private float _rootImmunityTimer;
	private AnimatedSprite2D _rootOverlay;
	private Vector2 _weaponDashDirection;
	private float _weaponDashDistanceRemaining;
	private float _weaponDashSpeed;
	private float _weaponDashTimeRemaining;


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

	// WeaponComponent child node (added in _Ready)
	private WeaponComponent _weaponComponent;

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
		CollisionLayer = 2; // Layer 2: Player
		CollisionMask = 5;  // Mask 1: World + Mask 3: Items/Doors

		_attackArea = GetNode<Area2D>("AttackArea");
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		SetupCombatPolish();

		health = MaxHealth;

		AddToGroup("player");

		GD.Print($"Player initialized: {health} HP");

		// ── WeaponComponent ──────────────────────────────────────────
		_weaponComponent = GetNodeOrNull<WeaponComponent>("WeaponComponent");
		if (_weaponComponent == null)
		{
			_weaponComponent = new WeaponComponent { Name = "WeaponComponent" };
			AddChild(_weaponComponent);
		}

		if (HUDScene == null)
		{
			HUDScene = GD.Load<PackedScene>("res://Scenes/Menus/GameHud.tscn");
		}

		if (HUDScene != null)
		{
			var hud = HUDScene.Instantiate();
			AddChild(hud);
		}

		HealthChanged?.Invoke(health, MaxHealth);
		MoneyChanged?.Invoke(Money);
		AddChild(GD.Load<PackedScene>("res://Scenes/Menus/DebugMenu.tscn").Instantiate());
	}

	// =========================
	// MOVEMENT & TIMERS
	// =========================
	
	
	private float _footstepTimer = 0.0f;
	private float _footstepInterval = 0.35f;
	  
	
	public override void _PhysicsProcess(double delta)
	{
		if (_bossIntroLocked)
		{
			Velocity = Vector2.Zero;
			_weaponDashDistanceRemaining = 0.0f;
			MoveAndSlide();
			return;
		}

		if (_invulnerabilityTimer > 0)
			_invulnerabilityTimer -= (float)delta;
		if (_rootImmunityTimer > 0.0f)
			_rootImmunityTimer = Mathf.Max(0.0f, _rootImmunityTimer - (float)delta);
		if (_stunTimer > 0.0f)
		{
			_stunTimer = Mathf.Max(0.0f, _stunTimer - (float)delta);
			Velocity = Vector2.Zero;
			_weaponDashDistanceRemaining = 0.0f;
			MoveAndSlide();
			return;
		}
		if (_rooted)
		{
			_rootTimer -= (float)delta;
			if (_rootTimer <= 0.0f)
				EndRoot();
		}

		UpdatePotion((float)delta);
		UpdateCameraShake((float)delta);

		if (_weaponDashDistanceRemaining > 0.0f)
		{
			float dashDelta = (float)delta;
			float requestedStep = Mathf.Min(_weaponDashSpeed * dashDelta, _weaponDashDistanceRemaining);
			Velocity = _weaponDashDirection * (requestedStep / Mathf.Max(dashDelta, 0.0001f));
			Vector2 positionBeforeDashStep = GlobalPosition;
			MoveAndSlide();
			float distanceTravelled = positionBeforeDashStep.DistanceTo(GlobalPosition);
			_weaponDashDistanceRemaining = Mathf.Max(0.0f, _weaponDashDistanceRemaining - distanceTravelled);
			_weaponDashTimeRemaining = Mathf.Max(0.0f, _weaponDashTimeRemaining - dashDelta);
	
			bool hitObstacle = GetSlideCollisionCount() > 0;
			bool dashFinished = hitObstacle
				|| _weaponDashDistanceRemaining <= 0.0f
				|| _weaponDashTimeRemaining <= 0.0f;
			if (dashFinished)
			{
				_weaponDashDistanceRemaining = 0.0f;
				_weaponDashTimeRemaining = 0.0f;
				_weaponDashSpeed = 0.0f;
				Velocity = Vector2.Zero;
			}
			else
			{
				return;
			}
		}
	
		Vector2 direction = Input.GetVector(
			"ui_left",
			"ui_right",
			"ui_up",
            "ui_down"
		);
		if (_rooted)
			direction = Vector2.Zero;

		if (!_isAttacking && direction.X != 0)
		{
			bool facingLeft = direction.X < 0;
			_animatedSprite.FlipH = facingLeft;
			_attackArea.Scale = new Vector2(facingLeft ? -1 : 1, 1);
		}

		if (direction != Vector2.Zero)
		{
			float moveSpeed = Speed + (ActivePotion == ShopConsumable.Speed ? 20f : 0f);
			Velocity = direction.Normalized() * moveSpeed;
		}
		else
		{
			Velocity = Vector2.Zero;
		}

		if (!_isAttacking)
		{
		if (direction != Vector2.Zero)
		{
		_animatedSprite.Play("walk");

		// Footstep Timer Countdown
		_footstepTimer -= (float)delta;
		if (_footstepTimer <= 0.0f)
		{
			AudioStreamPlayer2D footstepNode = GetNode<AudioStreamPlayer2D>("PlayerFootstep");
			footstepNode.Stop();
			footstepNode.Play();
			_footstepTimer = _footstepInterval; // Resets countdown for next step
		}
	}
	else
	{
		_animatedSprite.Play("idle");
		_footstepTimer = 0.0f; // Reset instantly when standing still
	}
}

		MoveAndSlide();
	}

	// =========================
	// PLAYER ATTACK
	// =========================

	public override void _Input(InputEvent @event)
	{
		if (_bossIntroLocked || _stunTimer > 0.0f)
			return;
		if (@event is InputEventKey dropKey && dropKey.Pressed && !dropKey.Echo && dropKey.Keycode == Key.G)
		{
			_weaponComponent?.DropCurrentWeapon();
			return;
		}

		// ── Weapon swap (Q key) ──────────────────────────────────────
		if (@event is InputEventKey swapKey
			&& swapKey.Pressed
			&& !swapKey.Echo
			&& swapKey.Keycode == Key.Q)
		{
			_weaponComponent?.SwapWeapon();
		}

		// ── Attack ───────────────────────────────────────────────────
		if (@event.IsActionPressed("attack"))
		{
			Vector2 mousePos = GetGlobalMousePosition();
			Vector2 aimDir = (mousePos - GlobalPosition).Normalized();
			if (_weaponComponent != null && _weaponComponent.CanAttack())
			{
				_weaponComponent.BeginAttack(aimDir);
				GetNode<AudioStreamPlayer2D>("HitSound").Play();
			}
			else if (_weaponComponent == null && !_isAttacking)
			{
				GD.Print("ATTACK DETECTED!");
				DoMeleeAttack(AttackDamage, null, aimDir);
				GetNode<AudioStreamPlayer2D>("HitSound").Play();
			}
		}

		if (@event.IsActionReleased("attack"))
		{
			Vector2 aimDir = (GetGlobalMousePosition() - GlobalPosition).Normalized();
			_weaponComponent?.ReleaseAttack(aimDir);
		}

		if (@event is InputEventKey dbKey
			&& dbKey.Pressed
			&& !dbKey.Echo
			&& dbKey.Keycode == Key.F2)
		{
			DatabaseManager database =
				GetNode<DatabaseManager>("/root/DatabaseManager");

			database.PrintDatabaseContents();
		}

		if (@event is InputEventKey key
			&& key.Pressed
			&& !key.Echo
			&& key.Keycode == Key.F3)
		{
			DatabaseManager database =
				GetNode<DatabaseManager>("/root/DatabaseManager");

			database.ClearDatabase();
		}
	}

	public void DoMeleeAttack(float damage, WeaponData weapon, Vector2 direction)
	{
		PlayWeaponAttack(weapon, direction);
		DealDamage(damage * WeaponDamageMultiplier);
	}

	/// <summary>Moves the player forward as part of a weapon's attack.</summary>
	public void PerformWeaponDash(Vector2 direction, float distance, float duration)
	{
		if (distance <= 0.0f || duration <= 0.0f || health <= 0.0f)
			return;

		if (direction.LengthSquared() <= 0.001f)
			direction = _animatedSprite.FlipH ? Vector2.Left : Vector2.Right;

		_weaponDashDirection = direction.Normalized();
		_weaponDashDistanceRemaining = distance;
		_weaponDashTimeRemaining = duration;
		_weaponDashSpeed = distance / duration;
	}

	private async void DealDamage(float damage)
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
				enemy.TakeDamage(damage, GlobalPosition);
			}
		}
	}

	// =========================
	// PLAYER TAKES DAMAGE
	// =========================

	public void TakeDamage(float damage)
	{
		if (_bossIntroLocked || _godMode || Invincible)
			return;

		if (damage <= 0 || health <= 0)
			return;

		if (_invulnerabilityTimer > 0)
			return;

		if (HasShield)
		{
			HasShield = false;
			QueueRedraw();
			_invulnerabilityTimer = 0.15f;
			CombatEffects.Burst(this, CombatEffects.Shield, amount: 16);
			ShakeCamera(1.2f);
			return;
		}

		ShakeCamera(1.8f);
		CombatEffects.Burst(this, CombatEffects.Impact);
		health = Mathf.Max(health - damage, 0);
		_invulnerabilityTimer = DamageInvulnerabilityTime;

		SpawnDamageNumber(damage);

		EndAttackPose();
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

	public void BeginBossIntroLock()
	{
		if (_bossIntroLocked)
			return;
		_bossIntroLocked = true;
		_bossIntroWasInvincible = Invincible;
		Invincible = true;
		Velocity = Vector2.Zero;
		_weaponDashDistanceRemaining = 0.0f;
		_weaponDashTimeRemaining = 0.0f;
		_weaponComponent?.CancelForBossEntrance();
	}

	public bool TryApplyRoot(float duration)
	{
		if (_rooted || _rootImmunityTimer > 0.0f || health <= 0.0f || _bossIntroLocked)
			return false;
		_rooted = true;
		_rootTimer = duration;
		_weaponDashDistanceRemaining = 0.0f;
		_weaponDashTimeRemaining = 0.0f;
		if (GodotObject.IsInstanceValid(_rootOverlay))
			_rootOverlay.QueueFree();
		_rootOverlay = new AnimatedSprite2D
		{
			Name = "RootWrapOverlay",
			SpriteFrames = BossSpriteFramesBuilder.ProjectileFrames(),
			Animation = "root_wrap",
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			Position = Vector2.Zero,
			ZIndex = 3,
		};
		AddChild(_rootOverlay);
		_rootOverlay.AnimationFinished += OnRootOverlayAnimationFinished;
		_rootOverlay.Play("root_wrap");
		return true;
	}

	public bool TryApplyStun(float duration)
	{
		if (health <= 0.0f || _bossIntroLocked || duration <= 0.0f)
			return false;
		_stunTimer = Mathf.Max(_stunTimer, duration);
		_weaponDashDistanceRemaining = 0.0f;
		_weaponDashTimeRemaining = 0.0f;
		_weaponComponent?.CancelForBossEntrance();
		_animatedSprite?.Play("hit");
		return true;
	}

	private void EndRoot()
	{
		if (!_rooted)
			return;
		_rooted = false;
		_rootTimer = 0.0f;
		_rootImmunityTimer = BossRoster.ROOT_IMMUNITY;
		if (GodotObject.IsInstanceValid(_rootOverlay))
			_rootOverlay.Play("root_break");
	}

	private void OnRootOverlayAnimationFinished()
	{
		if (!GodotObject.IsInstanceValid(_rootOverlay))
			return;
		if (_rootOverlay.Animation == "root_wrap")
			_rootOverlay.Pause();
		else if (_rootOverlay.Animation == "root_break")
		{
			_rootOverlay.QueueFree();
			_rootOverlay = null;
		}
	}

	public void ClearRootEffect()
	{
		_rooted = false;
		_rootTimer = 0.0f;
		_rootImmunityTimer = 0.0f;
		if (GodotObject.IsInstanceValid(_rootOverlay))
			_rootOverlay.QueueFree();
		_rootOverlay = null;
	}

	public void EndBossIntroLock()
	{
		if (!_bossIntroLocked)
			return;
		_bossIntroLocked = false;
		Invincible = _bossIntroWasInvincible;
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
		ClearRootEffect();
		GD.Print("PLAYER DIED!");

		Velocity = Vector2.Zero;
		if (_shakeCamera != null)
			_shakeCamera.Offset = _restingCameraOffset;
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

	public void AddGold(int amount) => CollectMoney(amount);

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
				Speed += 20.0f;
				SpeedUpgradeLevel++;

				GD.Print($"Movement speed increased to {Speed}");
				break;

			case "max_health_up":
				MaxHealth += 25.0f;
				health += 25.0f;
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
