using Godot;

public partial class OrcKingAxe : Area2D
{
	private AnimatedSprite2D _sprite;
	private OrcKing _owner;
	private Vector2 _direction;
	private float _remaining;
	private bool _returning;
	private bool _hit;
	private float _damage = BossRoster.ORC_KING_THROW_DAMAGE;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 1 | 2;
		_sprite = new AnimatedSprite2D { TextureFilter = CanvasItem.TextureFilterEnum.Nearest, Scale = Vector2.One * 1.5f, ZIndex = 5 };
		var sheet = GD.Load<Texture2D>("res://Assets/Enemies/OrcKing/orc_king_axe_projectile.png");
		var frames = new SpriteFrames(); frames.RemoveAnimation("default"); frames.AddAnimation("spin");
		frames.SetAnimationLoopMode("spin", SpriteFrames.LoopMode.Linear); frames.SetAnimationSpeed("spin", 12);
		for (int i = 0; i < 4; i++) frames.AddFrame("spin", new AtlasTexture { Atlas = sheet, Region = new Rect2(i * 40, 0, 40, 40), FilterClip = true });
		_sprite.SpriteFrames = frames; _sprite.Play("spin"); AddChild(_sprite);
		var shape = new CollisionShape2D { Shape = new CircleShape2D { Radius = 11 } }; AddChild(shape);
		BodyEntered += OnBodyEntered;
		_remaining = BossRoster.ORC_KING_AXE_LIFETIME;
	}

	public void Setup(Vector2 target, OrcKing owner, float damageMultiplier = 1.0f)
	{
		_owner = owner;
		_damage = BossRoster.ORC_KING_THROW_DAMAGE * damageMultiplier;
		_direction = (_owner != null ? _owner.GlobalPosition : GlobalPosition).DirectionTo(target);
		if (_direction == Vector2.Zero) _direction = Vector2.Right;
	}

	public override void _PhysicsProcess(double delta)
	{
		_remaining -= (float)delta;
		if (_remaining <= 0) { Finish(); return; }
		// CanvasTransform maps world/canvas coordinates into viewport pixels.
		Vector2 screenPosition = GetViewport().GetCanvasTransform() * GlobalPosition;
		if (!GetViewport().GetVisibleRect().Grow(80).HasPoint(screenPosition)) { Finish(); return; }
		if (_returning && GodotObject.IsInstanceValid(_owner)) _direction = GlobalPosition.DirectionTo(_owner.GlobalPosition);
		GlobalPosition += _direction * BossRoster.ORC_KING_THROW_SPEED * (float)delta;
		Rotation += 9.0f * (float)delta;
		if (_returning && GodotObject.IsInstanceValid(_owner) && GlobalPosition.DistanceTo(_owner.GlobalPosition) < 22) Finish();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_hit) return;
		if (body == _owner) return;
		if (body is Player player)
		{
			_hit = true; player.TakeDamage(_damage, _owner);
			if (ReturnsToOwner()) _returning = true; else Finish();
		}
		else if (body is TileMapLayer || body is StaticBody2D) Finish();
	}

	private static bool ReturnsToOwner() => BossRoster.ORC_KING_AXE_RETURNS;

	private void Finish()
	{
		if (GodotObject.IsInstanceValid(_owner)) _owner.AxeReturned(this);
		QueueFree();
	}
}
