using Godot;

/// <summary>
/// Player-fired projectile. Travels in a direction, damages enemies on contact.
/// Mirrors EnemyProjectile but hits the Enemy layer instead.
/// </summary>
public partial class PlayerProjectile : Area2D
{
	[Export] public float Speed    = 400.0f;
	[Export] public float Damage   = 12.0f;
	[Export] public float Lifetime = 2.5f;

	/// <summary>Optionally override the sprite texture at runtime.</summary>
	public Texture2D OverrideTexture { get; set; }

	private Vector2 _direction;
	private bool    _hasHit = false;
	private Sprite2D _sprite;

	public void Setup(Vector2 direction, float damage, float speed)
	{
		_direction = direction.Normalized();
		Damage     = damage;
		Speed      = speed;

		// Rotate arrow/bolt toward travel direction
		Rotation = _direction.Angle() + Mathf.Pi / 2.0f;
	}

	public override void _Ready()
	{
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");

		if (_sprite != null && OverrideTexture != null)
			_sprite.Texture = OverrideTexture;

		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_hasHit) return;

		GlobalPosition += _direction * Speed * (float)delta;

		Lifetime -= (float)delta;
		if (Lifetime <= 0.0f)
			QueueFree();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_hasHit) return;

		if (body is Enemy enemy)
		{
			_hasHit = true;
			enemy.TakeDamage(Damage, GlobalPosition);
			QueueFree();
		}
		else if (body is TileMapLayer || body is StaticBody2D)
		{
			_hasHit = true;
			CombatEffects.Burst(this, CombatEffects.Impact, direction: -_direction);
			QueueFree();
		}
	}
}
