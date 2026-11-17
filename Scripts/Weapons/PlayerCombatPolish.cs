using Godot;
using RogueBound.Dungeon;
using RogueBound.Weapons;

public partial class Player
{
	[Export(PropertyHint.Range, "0,3,0.1")] public float ScreenShakeStrength = 1f;

	private const int AttackFrames = 6;
	private Sprite2D _attackWeapon;
	private Camera2D _shakeCamera;
	private Vector2 _restingCameraOffset;
	private float _shakeRemaining;
	private float _shakeAmplitude;
	private float _regenElapsed;

	public const float PotionDuration = 10f;
	public ShopConsumable ActivePotion { get; private set; }
	public float ActivePotionTimeRemaining { get; private set; }
	public bool HasShield { get; private set; }
	public float WeaponDamageMultiplier => ActivePotion == ShopConsumable.Strength ? 1.5f : 1f;

	private void SetupCombatPolish()
	{
		_shakeCamera = GetNodeOrNull<Camera2D>("Camera2D");
		if (_shakeCamera != null)
			_restingCameraOffset = _shakeCamera.Offset;

		var poses = GD.Load<Texture2D>("res://Assets/Combat/player_attack_poses.png");
		var frames = (SpriteFrames)_animatedSprite.SpriteFrames.Duplicate();
		string[] animations = { "swing", "thrust", "cast" };
		for (int row = 0; row < animations.Length; row++)
		{
			string animation = animations[row];
			frames.AddAnimation(animation);
			frames.SetAnimationLoopMode(animation, SpriteFrames.LoopMode.None);
			frames.SetAnimationSpeed(animation, 25);
			for (int frame = 0; frame < AttackFrames; frame++)
				frames.AddFrame(animation, new AtlasTexture { Atlas = poses, Region = new Rect2(frame * 100, row * 100, 100, 100) });
		}
		_animatedSprite.SpriteFrames = frames;
		_animatedSprite.Play("idle");
		_animatedSprite.AnimationFinished += EndAttackPose;
		_animatedSprite.FrameChanged += UpdateWeaponPose;

		_attackWeapon = new Sprite2D
		{
			Name = "AttackWeapon",
			Texture = GD.Load<Texture2D>("res://Assets/Combat/weapon_attack_poses.png"),
			Hframes = AttackFrames,
			Vframes = 5,
			ZIndex = 1,
			Visible = false
		};
		AddChild(_attackWeapon);
	}

	public void PlayWeaponAttack(WeaponData weapon, Vector2 direction)
	{
		if (!Mathf.IsZeroApprox(direction.X))
			_animatedSprite.FlipH = direction.X < 0;
		_attackArea.Scale = new Vector2(_animatedSprite.FlipH ? -1 : 1, 1);
		int row = weapon?.Id switch
		{
			"battleaxe" => 1,
			"dagger" => 2,
			"bow" => 3,
			"staff" => 4,
			_ => 0
		};
		_animatedSprite.Stop();
		_isAttacking = true;
		_attackWeapon.Visible = weapon != null;
		_attackWeapon.FlipH = _animatedSprite.FlipH;
		_attackWeapon.Position = _animatedSprite.Position + new Vector2(_animatedSprite.FlipH ? -7 : 7, 2);
		_attackWeapon.FrameCoords = new Vector2I(0, row);
		string animation = weapon == null ? "attack" : row switch
		{
			2 => "thrust",
			3 or 4 => "cast",
			_ => "swing"
		};
		_animatedSprite.Play(animation);
		ShakeCamera(row == 1 || row == 4 ? 1.6f : 0.9f);
		CombatEffects.Burst(this, row == 4 ? CombatEffects.Magic : Colors.Bisque, direction * 10f, direction);
	}

	private void UpdateWeaponPose()
	{
		if (_attackWeapon == null || !_attackWeapon.Visible)
			return;
		_attackWeapon.FrameCoords = new Vector2I(Mathf.Min(_animatedSprite.Frame, AttackFrames - 1), _attackWeapon.FrameCoords.Y);
	}

	public void EndAttackPose()
	{
		_isAttacking = false;
		_attackWeapon?.Hide();
	}

	public void ShakeCamera(float amplitude)
	{
		if (_shakeCamera == null || ScreenShakeStrength <= 0f || health <= 0f)
			return;
		_shakeAmplitude = Mathf.Max(amplitude, _shakeAmplitude * (_shakeRemaining / 0.13f));
		_shakeRemaining = 0.13f;
	}

	private void UpdateCameraShake(float delta)
	{
		if (_shakeRemaining <= 0f)
			return;
		_shakeRemaining = Mathf.Max(0f, _shakeRemaining - delta);
		float strength = _shakeAmplitude * ScreenShakeStrength * (_shakeRemaining / 0.13f);
		_shakeCamera.Offset = _restingCameraOffset + new Vector2(GD.Randf() * 2f - 1f, GD.Randf() * 2f - 1f) * strength;
	}

	public void UseConsumable(ShopConsumable item)
	{
		if (item == ShopConsumable.None)
			return;
		if (item == ShopConsumable.Shield)
		{
			HasShield = true;
			QueueRedraw();
			CombatEffects.Burst(this, CombatEffects.Shield);
			return;
		}

		ActivePotion = item;
		ActivePotionTimeRemaining = PotionDuration;
		_regenElapsed = 0f;
		Color color = item switch
		{
			ShopConsumable.Speed => CombatEffects.Magic,
			ShopConsumable.Strength => CombatEffects.Impact,
			_ => Colors.LightGreen
		};
		CombatEffects.Burst(this, color, amount: 14);
	}

	private void UpdatePotion(float delta)
	{
		if (ActivePotionTimeRemaining <= 0f)
			return;
		float step = Mathf.Min(delta, ActivePotionTimeRemaining);
		ActivePotionTimeRemaining -= step;
		if (ActivePotion == ShopConsumable.Regeneration)
		{
			_regenElapsed += step;
			if (_regenElapsed >= 1f || ActivePotionTimeRemaining <= 0f)
			{
				if (health < MaxHealth)
					Heal(3f * _regenElapsed);
				_regenElapsed = 0f;
			}
		}
		if (ActivePotionTimeRemaining <= 0f)
			ActivePotion = ShopConsumable.None;
	}

	public override void _Draw()
	{
		if (!HasShield)
			return;
		DrawCircle(Vector2.Zero, 16f, new Color(0.45f, 1f, 0.85f, 0.18f));
		DrawArc(Vector2.Zero, 16f, 0f, Mathf.Tau, 32, CombatEffects.Shield, 1.2f, true);
	}
}
