using Godot;

public partial class GolemRock : Area2D
{
	private enum RockState { Flying, GroundHazard }

	private static readonly Color Transparent = new(0, 0, 0, 0);
	private static readonly Color Ink = new("#1e1b24");
	private static readonly Color StoneDark = new("#4a4a58");
	private static readonly Color Stone = new("#6e6e80");
	private static readonly Color StoneLight = new("#9a9aae");
	private static ImageTexture _rockTexture;

	[Export] public float GroundDuration = 2.4f;

	private Sprite2D _sprite;
	private CollisionShape2D _collisionShape;
	private Vector2 _direction = Vector2.Right;
	private float _damage = 10.0f;
	private float _speed = 120.0f;
	private float _timer = 1.0f;
	private bool _isBoulder;
	private bool _damageSpent;
	private RockState _state = RockState.Flying;
	private GolemMiniboss _source;
	private float _trailTimer;

	public void Setup(Vector2 direction, float damage, float speed, float flightTime, bool isBoulder, GolemMiniboss source)
	{
		_direction = direction.LengthSquared() > 0.001f ? direction.Normalized() : Vector2.Right;
		_damage = damage;
		_speed = speed;
		_timer = Mathf.Max(0.1f, flightTime);
		_isBoulder = isBoulder;
		_source = source;
	}

	public override void _Ready()
	{
		AddToGroup("floor_items");
		CollisionLayer = 0;
		CollisionMask = 2; // Player; the aimed flight timer provides the landing point.
		Monitoring = true;
		BodyEntered += OnBodyEntered;

		_sprite = GetNode<Sprite2D>("Sprite2D");
		_sprite.Texture = GetRockTexture();
		_sprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		_sprite.Scale = _isBoulder ? new Vector2(2, 2) : Vector2.One;

		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		_collisionShape.Shape = new CircleShape2D { Radius = _isBoulder ? 11.0f : 6.0f };

	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_timer -= dt;
		if (_state == RockState.GroundHazard)
		{
			if (_timer <= 0.0f)
				QueueFree();
			return;
		}

		GlobalPosition += _direction * _speed * dt;
		_sprite.Rotation += dt * (_isBoulder ? 3f : 6f);
		if (_isBoulder)
		{
			_trailTimer -= dt;
			if (_trailTimer <= 0)
			{
				_trailTimer = 0.12f;
				CombatEffects.Burst(this, Colors.SandyBrown, direction: -_direction, amount: 3);
			}
		}
		if (_timer <= 0.0f)
		{
			if (_isBoulder)
				LandBoulder();
			else
				SettleAsHazard();
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_state == RockState.GroundHazard)
		{
			DamagePlayerOnce(body);
			return;
		}

		if (body is Player)
		{
			DamagePlayerOnce(body);
			if (_isBoulder)
				LandBoulder();
			else
				QueueFree();
			return;
		}

		if (body is TileMapLayer || body is StaticBody2D)
		{
			if (_isBoulder)
				LandBoulder();
			else
				SettleAsHazard();
		}
	}

	private void DamagePlayerOnce(Node body)
	{
		if (_damageSpent || body is not Player player)
			return;
		_damageSpent = true;
		player.TakeDamage(_damage);
		CombatEffects.Burst(this, Colors.SandyBrown, amount: 10);
	}

	private void SettleAsHazard()
	{
		if (_state != RockState.Flying)
			return;
		_state = RockState.GroundHazard;
		_timer = GroundDuration;
		_direction = Vector2.Zero;
		_speed = 0.0f;
		CollisionMask = 2; // While resting, only the player can trigger the hazard.
		_sprite.Rotation = 0.0f;
		_sprite.Scale = Vector2.One;
		CombatEffects.Burst(this, Colors.Gray, amount: 5);
	}

	private void LandBoulder()
	{
		if (_state != RockState.Flying)
			return;
		_state = RockState.GroundHazard;
		GolemShockwave.Spawn(this, 60f, CombatEffects.Impact);
		CombatEffects.Burst(this, Colors.SandyBrown, amount: 22);
		(GetTree().GetFirstNodeInGroup("player") as Player)?.ShakeCamera(2.3f);
		ScatterGroundRocks();
		QueueFree();
	}

	private void ScatterGroundRocks()
	{
		PackedScene scene = GD.Load<PackedScene>("res://Scenes/Effects/GolemRock.tscn");
		if (scene == null || GetTree().CurrentScene == null)
			return;

		var world = GetTree().CurrentScene;
		Vector2 position = GlobalPosition;
		float damage = _damage * 0.65f;
		GolemMiniboss source = _source;
		Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(world) || world.IsQueuedForDeletion())
				return;
			const int rockCount = 8;
			for (int i = 0; i < rockCount; i++)
			{
				Vector2 direction = Vector2.FromAngle(Mathf.Tau * i / rockCount);
				var rock = scene.Instantiate<GolemRock>();
				rock.Setup(direction, damage, 150.0f, 0.55f, false, source);
				rock.Position = position;
				world.AddChild(rock);
			}
		}).CallDeferred();
	}

	private static ImageTexture GetRockTexture()
	{
		if (_rockTexture != null)
			return _rockTexture;

		string[] pixels =
		{
			"....00000000....",
			"...0111111110...",
			"..012222222210..",
			".01223333332210.",
			"0122233333332210",
			"0122333333332210",
			"0122233333332210",
			".01222333332210.",
			"..012222222210..",
			"...0111111110...",
			"....00000000....",
			"................",
			"................",
			"................",
			"................",
			"................"
		};
		Color[] palette = { Ink, StoneDark, Stone, StoneLight };
		Image image = Image.CreateEmpty(16, 16, true, Image.Format.Rgba8);
		image.Fill(Transparent);
		for (int y = 0; y < 16; y++)
		{
			for (int x = 0; x < 16; x++)
			{
				char pixel = pixels[y][x];
				if (pixel != '.')
					image.SetPixel(x, y, palette[pixel - '0']);
			}
		}

		_rockTexture = ImageTexture.CreateFromImage(image);
		return _rockTexture;
	}
}
