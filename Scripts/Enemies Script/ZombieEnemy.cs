using Godot;

public partial class ZombieEnemy : Enemy
{
	[Export] public float AttackRange = 26.0f;
	private bool _isAttacking = false;

	public override void _Ready()
	{
		base._Ready();

		// Zombie-specific default stats.
		Speed = 35.0f;
		MaxHealth = 55.0f;
		_health = MaxHealth;
		AttackDamage = 18.0f;
		AttackCooldown = 1.3f;
		KnockbackForce = 150.0f;
		StopDistance = 22.0f;
	}

	protected override void TryAttackPlayer()
	{
		if (_attackTimer > 0.0f || _player == null || _isDead || _isHit || _isAttacking)
			return;

		float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);

		if (distance <= AttackRange)
		{
			FaceDirection(_player.GlobalPosition.X - GlobalPosition.X);
			_isAttacking = true;
			_attackTimer = AttackCooldown;
			Velocity = Vector2.Zero;

			if (_animatedSprite.SpriteFrames.HasAnimation("attack"))
				_animatedSprite.Play("attack");

			GetNodeOrNull<AudioStreamPlayer2D>("HitBoxSound")?.Play();

			if (_player is Player player)
			{
				player.TakeDamage(AttackDamage);
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isAttacking && !_isDead && !_isHit)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		base._PhysicsProcess(delta);
	}

	public override void TakeDamage(float damage, Vector2 attackerPosition)
	{
		_isAttacking = false;
		base.TakeDamage(damage, attackerPosition);
	}

	protected override void OnAnimationFinished()
	{
		if (_isDead)
		{
			base.OnAnimationFinished();
			return;
		}

		if (_animatedSprite.Animation == "attack")
		{
			_isAttacking = false;
			_animatedSprite.Play("walk");
			return;
		}

		base.OnAnimationFinished();
	}
}
