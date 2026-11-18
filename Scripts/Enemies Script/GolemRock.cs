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

	[Export] public float GroundDuration = GolemBossSettings.SMALL_ROCK_GROUND_SECONDS;

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
	private Polygon2D _warning;
	private Polygon2D _warningCore;

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
		AddToGroup("golem_rocks");
		CollisionLayer = 0;
		CollisionMask = 2; // Player; the aimed flight timer provides the landing point.
		Monitoring = true;
		BodyEntered += OnBodyEntered;

		_sprite = GetNode<Sprite2D>("Sprite2D");
		_sprite.Texture = GetRockTexture();
		_sprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		_sprite.Scale = _isBoulder ? Vector2.One * GolemBossSettings.BOULDER_RENDER_SCALE : Vector2.One;

		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		_collisionShape.Shape = new CircleShape2D { Radius = _isBoulder ? 11.0f : 6.0f };
		if (_isBoulder)
		{
			_warning = new Polygon2D { Name = "LandingWarning", Color = new Color(GolemBossSettings.ROCK_WARNING_COLOR), Visible = false, ZIndex = -1 };
			_warning.Polygon = MakeOctagon(GolemBossSettings.ROCK_WARNING_RADIUS);
			_warningCore = new Polygon2D { Color = new Color(GolemBossSettings.ROCK_WARNING_CORE_COLOR) };
			_warningCore.Polygon = MakeOctagon(GolemBossSettings.ROCK_WARNING_RADIUS * 0.55f);
			_warning.AddChild(_warningCore);
			AddChild(_warning);
		}
	}

	public bool BelongsTo(GolemMiniboss boss) => _source == boss;

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

		Vector2 start = GlobalPosition;
		Vector2 travel = _direction * _speed * dt;
		int samples = Mathf.Max(1, Mathf.CeilToInt(travel.Length() / GolemBossSettings.ROCK_SWEEP_SAMPLE_PIXELS));
		for (int i = 1; i <= samples; i++)
		{
			Vector2 sample = start + travel * ((float)i / samples);
			if (CheckSweptPlayerHit(sample)) return;
		}
		GlobalPosition = start + travel;
		if (_isBoulder && _timer <= GolemBossSettings.ROCK_LANDING_WARNING_SECONDS && _warning != null)
		{
			_warning.Visible = true;
			_warning.Position = _direction * _speed * _timer;
			bool bright = Mathf.Sin((float)Time.GetTicksMsec() * 0.001f * Mathf.Tau * GolemBossSettings.ROCK_WARNING_BLINK_HZ) > 0.0f;
			_warning.Color = new Color(bright ? GolemBossSettings.ROCK_WARNING_COLOR : GolemBossSettings.ROCK_WARNING_ALT_COLOR);
		}
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

	private bool CheckSweptPlayerHit(Vector2 position)
	{
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = _collisionShape.Shape,
			Transform = new Transform2D(0.0f, position),
			CollisionMask = 2,
			CollideWithAreas = false,
			CollideWithBodies = true,
		};
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		if (_source != null && GodotObject.IsInstanceValid(_source)) query.Exclude.Add(_source.GetRid());
		foreach (Godot.Collections.Dictionary hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit.TryGetValue("collider", out Variant collider) && collider.AsGodotObject() is Player player)
			{
				GlobalPosition = position;
				OnBodyEntered(player);
				return true;
			}
		}
		return false;
	}

	private static Vector2[] MakeOctagon(float radius)
	{
		Vector2[] points = new Vector2[8];
		for (int i = 0; i < points.Length; i++)
		{
			float angle = Mathf.Tau * i / points.Length;
			points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		}
		return points;
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

		int rockCount = GolemBossSettings.PEBBLE_COUNT;
		for (int i = 0; i < rockCount; i++)
		{
			float angle = Mathf.Tau * i / rockCount;
			Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
			GolemRock rock = scene.Instantiate<GolemRock>();
			rock.Setup(direction, _damage * GolemBossSettings.SMALL_ROCK_DAMAGE_RATIO_ON_SCATTER, GolemBossSettings.PEBBLE_SPEED, GolemBossSettings.PEBBLE_FLIGHT_SECONDS, false, _source);
			GetTree().CurrentScene.AddChild(rock);
			rock.GlobalPosition = GlobalPosition;
		}
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
