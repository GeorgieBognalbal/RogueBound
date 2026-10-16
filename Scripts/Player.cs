
using Godot;
using System;

public partial class Player : CharacterBody2D
{
    public const float Speed = 150.0f;
    public float health = 100.0f;

    private AnimatedSprite2D _animatedSprite;

    public override void _Ready()
    {
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

            _animatedSprite.Play("walk");

            if (direction.X != 0)
            {
                _animatedSprite.FlipH = direction.X < 0;
            }
        }
        else
        {
            Velocity = Vector2.Zero;
            _animatedSprite.Play("idle");
        }

        MoveAndSlide();
    }
}
