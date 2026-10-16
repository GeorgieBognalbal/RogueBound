
using Godot;
using System;

public partial class Player : CharacterBody2D
{
	public float Speed = 150.0f;
	public float health = 100.0f;

    [Export] public int AttackDamage = 10;

    private AnimatedSprite2D _animatedSprite;
    private bool _isAttacking = false;
    private Area2D _attackArea;

    public override void _Ready()
	{
        _attackArea = GetNode<Area2D>("AttackArea");
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

		// Add the player to the group the enemy searches for
		AddToGroup("player");
	}

    public override void _PhysicsProcess(double delta)
    {
        Vector2 direction = Input.GetVector(
            "ui_left",
            "ui_right",
            "ui_up",
            "ui_down"
        );

        if (direction != Vector2.Zero)
        {
            Velocity = direction.Normalized() * Speed;

            if (direction.X != 0)
            {
                _animatedSprite.FlipH = direction.X < 0;
            }
        }
        else
        {
            Velocity = Vector2.Zero;
        }

        // Only update movement animations when NOT attacking
        if (!_isAttacking)
        {
            if (direction != Vector2.Zero)
                _animatedSprite.Play("walk");
            else
                _animatedSprite.Play("idle");
        }

        MoveAndSlide();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("attack") && !_isAttacking)
        {
            GD.Print("ATTACK KEY DETECTED!");
            Attack();
        }
    }


    private async void Attack()
    {
        _isAttacking = true;
        _animatedSprite.Play("attack");

        DealDamage();

        await ToSignal(
            _animatedSprite,
            AnimatedSprite2D.SignalName.AnimationFinished
        );

        _isAttacking = false;
    }


    private async void DealDamage()
    {
        // Wait one physics frame so Godot can update overlaps
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        foreach (Node2D body in _attackArea.GetOverlappingBodies())
        {
            GD.Print($"Attack area detected: {body.Name}");

            if (body is Enemy enemy)
            {
                enemy.TakeDamage(AttackDamage, GlobalPosition);
            }
        }
    }
}
