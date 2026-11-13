using Godot;

public partial class OrcEnemy : Enemy
{
	[Export] public float AttackRange = 32.0f;
	private bool _isAttacking = false;

	public override void _Ready()
	{
		base._Ready();

		// Orc Brute default stats
		MaxHealth = 80.0f;
		_health = MaxHealth;
		Speed = 42.0f;
		AttackDamage = 22.0f;
		AttackCooldown = 1.2f;
		KnockbackForce = 120.0f; // Sturdy, harder to knock back
		StopDistance = 28.0f;
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

			_animatedSprite.Play("attack");

			// Play attack sound if available
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
