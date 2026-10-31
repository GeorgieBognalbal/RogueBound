using Godot;

public partial class ArcherEnemy : Enemy
{
	[Export] public PackedScene ProjectileScene;

	[Export] public float AttackRange = 150.0f;
	[Export] public float ShootCooldown = 1.5f;
	[Export] public float ProjectileDamage = 10.0f;
	[Export] public float ProjectileSpeed = 250.0f;

	public override void _Ready()
	{
		base._Ready();

		_navAgent.TargetDesiredDistance = AttackRange;
	}

	protected override void TryAttackPlayer()
	{
		if (_player == null || ProjectileScene == null)
			return;

		if (_attackTimer > 0.0f || _isHit || _isDead)
			return;

		float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);

		// Only attack when the player is within range.
		if (distance > AttackRange)
			return;

		// Face the player.
		FaceDirection(_player.GlobalPosition.X - GlobalPosition.X);

		// Play shooting animation.
		_animatedSprite.Play("shoot");

		// Fire projectile.
		ShootProjectile();

		_attackTimer = ShootCooldown;
	}

	private void ShootProjectile()
	{
		if (ProjectileScene == null || _player == null)
			return;

		EnemyProjectile projectile =
			ProjectileScene.Instantiate<EnemyProjectile>();

		Vector2 direction =
			GlobalPosition.DirectionTo(_player.GlobalPosition);

		projectile.Setup(
			direction,
			ProjectileDamage,
			ProjectileSpeed
		);

		GetTree().CurrentScene.AddChild(projectile);
		projectile.GlobalPosition = GlobalPosition;
	}

	protected override void OnAnimationFinished()
	{
		if (_isDead)
		{
			base.OnAnimationFinished();
			return;
		}

		if (_animatedSprite.Animation == "shoot")
		{
			_animatedSprite.Play("idle");
			return;
		}

		base.OnAnimationFinished();
	}
}
