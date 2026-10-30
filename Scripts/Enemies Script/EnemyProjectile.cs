using Godot;

public partial class EnemyProjectile : Area2D
{
	[Export] public float Speed = 250.0f;
	[Export] public float Damage = 10.0f;
	[Export] public float Lifetime = 4.0f;

	private Vector2 _direction;
	private bool _hasHit = false;

	public void Setup(Vector2 direction, float damage, float speed)
	{
		_direction = direction.Normalized();
		Damage = damage;
		Speed = speed;

		// Rotate the arrow toward its movement direction.
		Rotation = _direction.Angle() + Mathf.Pi / 2.0f;
	}

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_hasHit)
			return;

		// Move projectile.
		GlobalPosition += _direction * Speed * (float)delta;

		// Reduce projectile lifetime.
		Lifetime -= (float)delta;

		if (Lifetime <= 0.0f)
			QueueFree();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_hasHit)
			return;

		if (body is Player player)
		{
			_hasHit = true;

			player.TakeDamage(Damage);

			QueueFree();
		}
	}
}
