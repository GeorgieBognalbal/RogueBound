
using Godot;

public partial class SkeletonEnemy : Enemy
{
	[Export] public float BlockChance = 0.5f;
	[Export] public float BlockDamageReduction = 0.0f;

	[Export] public float BlockKnockbackForce = 80.0f;
	[Export] public float BlockHitStunDuration = 0.12f;

	public override void TakeDamage(float damage, Vector2 attackerPosition)
	{
		if (_isDead || damage <= 0.0f)
			return;

		Vector2 directionToAttacker =
			(attackerPosition - GlobalPosition).Normalized();

		Vector2 facingDirection = _animatedSprite.FlipH
			? Vector2.Left
			: Vector2.Right;

		bool attackerInFront =
			facingDirection.Dot(directionToAttacker) > 0.0f;

		if (attackerInFront && GD.Randf() < BlockChance)
		{
			float blockedDamage = damage * BlockDamageReduction;

			GD.Print("Skeleton Warrior blocked the attack!");

			// Apply a small knockback even when blocking all damage.
			Vector2 knockbackDirection =
				GlobalPosition - attackerPosition;

			if (knockbackDirection.LengthSquared() > 0.001f)
				knockbackDirection = knockbackDirection.Normalized();
			else
				knockbackDirection = Vector2.Right;

			_knockbackVelocity =
				knockbackDirection * BlockKnockbackForce;

			_hitTimer = BlockHitStunDuration;

			_animatedSprite.Play("block");

			if (blockedDamage <= 0.0f)
				return;

			damage = blockedDamage;
		}

		base.TakeDamage(damage, attackerPosition);
	}
}
