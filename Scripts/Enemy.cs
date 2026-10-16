
using Godot;

public partial class Enemy : CharacterBody2D
{
    [Export] public float Speed = 100.0f;
    [Export] public float StopDistance = 20.0f;
    [Export] public float RepathInterval = 0.2f;

    private Node2D _player;
    private NavigationAgent2D _navAgent;
    private AnimatedSprite2D _animatedSprite;

    private float _repathTimer;

    public override async void _Ready()
    {
        _animatedSprite =
            GetNode<AnimatedSprite2D>("EnemyAnimatedSprite");

        _player =
            GetTree().GetFirstNodeInGroup("player") as Node2D;

        _navAgent =
            GetNode<NavigationAgent2D>("EnemyNavigationAgent");

        _navAgent.PathDesiredDistance = 4.0f;
        _navAgent.TargetDesiredDistance = StopDistance;

        // Wait for the navigation map to synchronize.
        await ToSignal(
            GetTree(),
            SceneTree.SignalName.PhysicsFrame
        );

        if (_player != null)
        {
            _navAgent.TargetPosition = _player.GlobalPosition;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null)
        {
            _player =
                GetTree().GetFirstNodeInGroup("player") as Node2D;

            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        // Periodically update the player's target position.
        _repathTimer -= (float)delta;

        if (_repathTimer <= 0.0f)
        {
            _navAgent.TargetPosition = _player.GlobalPosition;
            _repathTimer = RepathInterval;
        }

        // Flip horizontally toward the player's position.
        float horizontalDifference =
            _player.GlobalPosition.X - GlobalPosition.X;

        if (horizontalDifference != 0.0f)
        {
            _animatedSprite.FlipH = horizontalDifference < 0.0f;
        }

        // Follow the navigation path.
        if (_navAgent.IsNavigationFinished())
        {
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        Vector2 nextPoint = _navAgent.GetNextPathPosition();

        Vector2 direction =
            GlobalPosition.DirectionTo(nextPoint);

        Velocity = direction * Speed;

        // Apply movement and physical collisions.
        MoveAndSlide();
    }
}
