using Godot;

public partial class Enemy : CharacterBody2D
{
    [Export] public float Speed = 50.0f;
    [Export] public float StopDistance = 20.0f;
    [Export] public float RepathInterval = 0.2f;

    [Export] public float MaxHealth = 30.0f;
    [Export] public float KnockbackForce = 250.0f;
    [Export] public float HitStunDuration = 0.2f;

    private Node2D _player;
    private NavigationAgent2D _navAgent;
    private AnimatedSprite2D _animatedSprite;

    private float _repathTimer;
    private float _health;
    private float _hitTimer;
    private Vector2 _knockbackVelocity;

    private bool _isDead = false;
    private bool _isHit = false;

    public override async void _Ready()
    {
        _health = MaxHealth;

        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;

        _navAgent = GetNode<NavigationAgent2D>("EnemyNavigationAgent");
        _animatedSprite = GetNode<AnimatedSprite2D>("EnemyAnimatedSprite");

        _navAgent.PathDesiredDistance = 4.0f;
        _navAgent.TargetDesiredDistance = StopDistance;

        _animatedSprite.AnimationFinished += OnAnimationFinished;

        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        if (_player != null && GodotObject.IsInstanceValid(_player))
        {
            _navAgent.TargetPosition = _player.GlobalPosition;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead)
            return;

        // Handle knockback and hit stun
        if (_hitTimer > 0.0f)
        {
            _hitTimer -= (float)delta;

            Velocity = _knockbackVelocity;

            _knockbackVelocity = _knockbackVelocity.MoveToward(
                Vector2.Zero,
                KnockbackForce * 5.0f * (float)delta
            );

            MoveAndSlide();
            return;
        }

        // Find the player again if necessary
        if (_player == null || !GodotObject.IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Node2D;

            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        // Update navigation target periodically
        _repathTimer -= (float)delta;

        if (_repathTimer <= 0.0f)
        {
            _navAgent.TargetPosition = _player.GlobalPosition;
            _repathTimer = RepathInterval;
        }

        // Stop when the enemy reaches the player
        if (_navAgent.IsNavigationFinished())
        {
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        // Follow the navigation path
        Vector2 nextPoint = _navAgent.GetNextPathPosition();
        Vector2 direction = GlobalPosition.DirectionTo(nextPoint);

        Velocity = direction * Speed;

        MoveAndSlide();

        // Play movement animation only when not playing a hit animation
        if (!_isHit && _animatedSprite.Animation.ToString() != "walk")
        {
            _animatedSprite.Play("walk");
        }

        // Face the direction of movement
        if (direction.X != 0)
        {
            _animatedSprite.FlipH = direction.X < 0;
        }
    }



    // DAMAGE LOGIC SECTION HEREEE
    // Called by the player's attack
    public void TakeDamage(float damage, Vector2 attackerPosition)
    {
        if (_isDead || _health <= 0.0f)
            return;

        _health -= damage;

        GD.Print($"Enemy took {damage} damage! HP: {Mathf.Max(_health, 0.0f)}/{MaxHealth}");

        // Death takes priority over hit animation
        if (_health <= 0.0f)
        {
            _isDead = true;
            _isHit = false;

            Velocity = Vector2.Zero;
            _knockbackVelocity = Vector2.Zero;

            _animatedSprite.Play("death");
            return;
        }

        // Calculate knockback direction
        Vector2 knockbackDirection = GlobalPosition - attackerPosition;

        if (knockbackDirection.LengthSquared() > 0.001f)
            knockbackDirection = knockbackDirection.Normalized();
        else
            knockbackDirection = Vector2.Right;

        _knockbackVelocity = knockbackDirection * KnockbackForce;
        _hitTimer = HitStunDuration;
        _isHit = true;

        // Face the attacker
        _animatedSprite.FlipH = attackerPosition.X > GlobalPosition.X;

        // Play the hit animation
        _animatedSprite.Play("hit");
    }

    // Called when a non-looping animation finishes
    private void OnAnimationFinished()
    {
        if (_isDead)
        {
            if (_animatedSprite.Animation == "death")
            {
                QueueFree();
            }

            return;
        }

        if (_animatedSprite.Animation == "hit")
        {
            _isHit = false;
            _animatedSprite.Play("walk");
        }
    }
}