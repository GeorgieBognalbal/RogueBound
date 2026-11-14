
using Godot;

public partial class SkeletonEnemy : Enemy
{
	[Export] public float BlockChance = 0.5f;
	[Export] public float BlockDamageReduction = 0.0f;

	[Export] public float BlockKnockbackForce = 80.0f;
	[Export] public float BlockHitStunDuration = 0.12f;
	[Export] public float BlockIndicatorDuration = 0.65f;

	private Label _blockIndicator;
	private float _blockIndicatorTimer;

	public override void _Ready()
	{
		base._Ready();

		_blockIndicator = new Label
		{
			Name = "BlockIndicator",
			Text = "BLOCKED!",
			Position = new Vector2(-42.0f, -42.0f),
			Size = new Vector2(84.0f, 20.0f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
			ZIndex = 10
		};
		_blockIndicator.AddThemeColorOverride("font_color", new Color(0.5f, 1.0f, 0.65f));
		_blockIndicator.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.12f, 0.07f));
		_blockIndicator.AddThemeConstantOverride("outline_size", 4);
		AddChild(_blockIndicator);
	}

	public override void _Process(double delta)
	{
		if (_blockIndicatorTimer <= 0.0f)
			return;

		_blockIndicatorTimer -= (float)delta;
		if (_blockIndicatorTimer <= 0.0f && _blockIndicator != null)
			_blockIndicator.Visible = false;
	}

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
			ShowBlockIndicator();

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

			if (_animatedSprite.SpriteFrames.HasAnimation("block"))
				_animatedSprite.Play("block");
			else
				_animatedSprite.Play("hit");

			if (blockedDamage <= 0.0f)
				return;

			damage = blockedDamage;
		}

		base.TakeDamage(damage, attackerPosition);
	}

	private void ShowBlockIndicator()
	{
		if (_blockIndicator == null)
			return;

		_blockIndicator.Visible = true;
		_blockIndicatorTimer = BlockIndicatorDuration;
	}
}
