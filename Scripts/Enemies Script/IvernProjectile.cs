using Godot;

public partial class IvernProjectile : Node2D
{
	private enum EffectKind { Vine, Poison }
	private enum PoisonPhase { Flying, Landing, Ground, Fading }

	private static SpriteFrames _projectileFrames;
	private EffectKind _kind;
	private PoisonPhase _poisonPhase;
	private IvernMiniboss _owner;
	private AnimatedSprite2D _sprite;
	private Vector2 _direction;
	private Vector2 _lastPosition;
	private Vector2 _targetPosition;
	private float _speed;
	private float _maxRange;
	private float _travelled;
	private float _flightElapsed;
	private float _flightDuration;
	private float _poisonTimer;
	private float _tickTimer;
	private bool _fading;
	private bool _ownerNotified;
	private bool _diagonalVine;

	public bool IsPoison { get; private set; }
	public bool IsFinished { get; private set; }

	public void SetupVine(Vector2 direction, IvernMiniboss owner)
	{
		_kind = EffectKind.Vine;
		_owner = owner;
		_direction = direction.Normalized();
		_speed = BossRoster.VINE_SPEED;
		_maxRange = BossRoster.VINE_MAX_RANGE;
	}

	public void SetupPoison(Vector2 target, IvernMiniboss owner)
	{
		_kind = EffectKind.Poison;
		_owner = owner;
		IsPoison = true;
		_targetPosition = target;
		_poisonPhase = PoisonPhase.Flying;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Pausable;
		AddToGroup("ivern_effects");
		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = _projectileFrames ??= BossSpriteFramesBuilder.ProjectileFrames(),
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			ZIndex = 2,
		};
		_sprite.AnimationFinished += OnSpriteAnimationFinished;
		AddChild(_sprite);
		_lastPosition = GlobalPosition;

		if (_kind == EffectKind.Vine)
		{
			SetVineSprite();
			_sprite.Play(_sprite.Animation);
		}
		else
		{
			float distance = GlobalPosition.DistanceTo(_targetPosition);
			_flightDuration = Mathf.Max(BossRoster.ANIMATION_FRAME_SECONDS,
				distance / BossRoster.POISON_SPEED);
			_sprite.Play("poison_fly");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_fading || IsFinished)
			return;

		if (_kind == EffectKind.Vine)
			StepVine(dt);
		else
			StepPoison(dt);
	}

	private void SetVineSprite()
	{
		bool diagonal = Mathf.Abs(_direction.X) > 0.2f && Mathf.Abs(_direction.Y) > 0.2f;
		_diagonalVine = diagonal;
		if (diagonal)
		{
			_sprite.Animation = "vine_diag";
			_sprite.FlipH = _direction.X < 0.0f;
			_sprite.FlipV = _direction.Y < 0.0f;
			_sprite.Rotation = 0.0f;
		}
		else if (Mathf.Abs(_direction.Y) > Mathf.Abs(_direction.X))
		{
			_sprite.Animation = "vine_straight";
			_sprite.RotationDegrees = 90.0f;
			_sprite.FlipH = _direction.Y < 0.0f;
		}
		else
		{
			_sprite.Animation = "vine_straight";
			_sprite.Rotation = 0.0f;
			_sprite.FlipH = _direction.X < 0.0f;
		}
	}

	private void StepVine(float dt)
	{
		float step = Mathf.Min(_speed * dt, _maxRange - _travelled);
		if (step <= 0.0f)
		{
			BeginVineFade();
			return;
		}
		Vector2 next = GlobalPosition + _direction * step;
		var result = CastSwept(GlobalPosition, next,
			_diagonalVine ? BossRoster.VINE_DIAGONAL_HALF_LENGTH : BossRoster.VINE_STRAIGHT_HALF_LENGTH,
			_diagonalVine ? BossRoster.VINE_DIAGONAL_HALF_WIDTH : BossRoster.VINE_STRAIGHT_HALF_WIDTH);
		if (result.Count > 0)
		{
			GlobalPosition = (Vector2)result["position"];
			if (result["collider"].AsGodotObject() is Player player)
			{
				player.TakeDamage(BossRoster.VINE_DAMAGE);
				player.TryApplyRoot(BossRoster.ROOT_DURATION);
			}
			BeginVineFade();
			return;
		}
		GlobalPosition = next;
		_travelled += step;
		if (_travelled >= _maxRange)
			BeginVineFade();
	}

	private void StepPoison(float dt)
	{
		switch (_poisonPhase)
		{
			case PoisonPhase.Flying:
				_flightElapsed += dt;
				float progress = Mathf.Clamp(_flightElapsed / _flightDuration, 0.0f, 1.0f);
				Vector2 next = _lastPosition.Lerp(_targetPosition, progress);
				var result = CastSwept(GlobalPosition, next, BossRoster.POISON_BLOB_HALF_LENGTH, BossRoster.POISON_BLOB_HALF_WIDTH);
				if (result.Count > 0)
				{
					GlobalPosition = (Vector2)result["position"];
					BeginPoisonLanding();
					return;
				}
				GlobalPosition = next;
				_sprite.Position = new Vector2(0.0f, -Mathf.Sin(progress * Mathf.Pi) * BossRoster.POISON_ARC_HEIGHT);
				if (progress >= 1.0f)
					BeginPoisonLanding();
				break;
			case PoisonPhase.Ground:
				_poisonTimer -= dt;
				_tickTimer -= dt;
				if (_tickTimer <= 0.0f)
				{
					_tickTimer += BossRoster.POISON_TICK_INTERVAL;
					var player = GetTree().GetFirstNodeInGroup("player") as Player;
					Vector2 playerOffset = player != null && GodotObject.IsInstanceValid(player)
						? player.GlobalPosition - GlobalPosition
						: new Vector2(float.MaxValue, float.MaxValue);
					if (player != null && GodotObject.IsInstanceValid(player) &&
						Mathf.Abs(playerOffset.X) <= BossRoster.POISON_PUDDLE_WIDTH * 0.5f + BossRoster.PLAYER_BODY_PADDING_X &&
						Mathf.Abs(playerOffset.Y) <= BossRoster.POISON_PUDDLE_HEIGHT * 0.5f + BossRoster.PLAYER_BODY_PADDING_Y)
						player.TakeDamage(BossRoster.POISON_TICK_DAMAGE);
				}
				if (_poisonTimer <= 0.0f)
					BeginPoisonFade();
				break;
		}
	}

	private Godot.Collections.Dictionary CastSwept(Vector2 from, Vector2 to, float halfLength, float halfWidth)
	{
		Vector2 travelDirection = from.DirectionTo(to);
		if (travelDirection == Vector2.Zero)
			return new Godot.Collections.Dictionary();
		Vector2 perpendicular = travelDirection.Orthogonal();
		foreach (float sideOffset in new[] { -halfWidth, 0.0f, halfWidth })
		{
			Vector2 rayStart = from - travelDirection * halfLength + perpendicular * sideOffset;
			Vector2 rayEnd = to + travelDirection * halfLength + perpendicular * sideOffset;
			var query = PhysicsRayQueryParameters2D.Create(rayStart, rayEnd, BossRoster.PROJECTILE_COLLISION_MASK);
			query.CollideWithAreas = false;
			if (_owner != null && GodotObject.IsInstanceValid(_owner))
				query.Exclude = new Godot.Collections.Array<Rid> { _owner.GetRid() };
			var result = GetWorld2D().DirectSpaceState.IntersectRay(query);
			if (result.Count > 0)
				return result;
		}
		return new Godot.Collections.Dictionary();
	}

	private void BeginVineFade()
	{
		if (_fading)
			return;
		_fading = true;
		_sprite.Play("vine_fade");
	}

	private void BeginPoisonLanding()
	{
		if (_poisonPhase != PoisonPhase.Flying)
			return;
		_poisonPhase = PoisonPhase.Landing;
		_sprite.Position = Vector2.Zero;
		_sprite.Play("poison_land");
	}

	private void BeginPoisonFade()
	{
		if (_poisonPhase == PoisonPhase.Fading)
			return;
		_poisonPhase = PoisonPhase.Fading;
		_sprite.Play("poison_fade");
	}

	private void OnSpriteAnimationFinished()
	{
		if (_kind == EffectKind.Vine && _fading)
		{
			Finish();
			return;
		}
		if (_kind == EffectKind.Poison && _poisonPhase == PoisonPhase.Landing)
		{
			_poisonPhase = PoisonPhase.Ground;
			_poisonTimer = BossRoster.POISON_DURATION;
			_tickTimer = BossRoster.POISON_TICK_INTERVAL;
			_sprite.Play("poison_ground");
		}
		else if (_kind == EffectKind.Poison && _poisonPhase == PoisonPhase.Fading)
			Finish();
	}

	private void Finish()
	{
		if (IsFinished)
			return;
		IsFinished = true;
		if (IsPoison && !_ownerNotified && _owner != null && GodotObject.IsInstanceValid(_owner))
		{
			_ownerNotified = true;
			_owner.OnPoisonEffectFinished(this);
		}
		QueueFree();
	}

	public void RemoveImmediately()
	{
		if (IsFinished)
			return;
		IsFinished = true;
		if (IsPoison && !_ownerNotified && _owner != null && GodotObject.IsInstanceValid(_owner))
		{
			_ownerNotified = true;
			_owner.OnPoisonEffectFinished(this);
		}
		QueueFree();
	}
}
