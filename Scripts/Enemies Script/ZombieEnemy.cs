
using Godot;

public partial class ZombieEnemy : Enemy
{
	[Export] public float AttackRange = 35.0f;

	public override void _Ready()
	{
		base._Ready();

		// Zombie-specific default stats.
		Speed = 35.0f;
		MaxHealth = 60.0f;
		AttackDamage = 20.0f;
		AttackCooldown = 1.5f;
		KnockbackForce = 150.0f;
	}

	protected override void TryAttackPlayer()
	{
		if (_attackTimer > 0.0f || _player == null)
			return;

		float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);

		if (distance <= AttackRange)
		{
			if (_player is Player player)
			{
				player.TakeDamage(AttackDamage);
				_attackTimer = AttackCooldown;

				// _animatedSprite.Play("attack");
			}
		}
	}
}
