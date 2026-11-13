using Godot;

public partial class Pickup : Area2D
{
	public enum PickupType
	{
		Coin,
		Health
	}

	[Export] public PickupType Type = PickupType.Coin;
	[Export] public int Value = 5;
	[Export] public float MagnetRange = 70.0f;
	[Export] public float MagnetSpeed = 180.0f;

	private Player _player;
	private Vector2 _initialPosition;
	private float _bobTimer = 0.0f;
	private bool _collected = false;

	public override void _Ready()
	{
		_initialPosition = Position;
		_player = GetTree().GetFirstNodeInGroup("player") as Player;

		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_collected)
			return;

		float dt = (float)delta;
		_bobTimer += dt * 4.0f;

		// Find player if null
		if (_player == null || !GodotObject.IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Player;
		}

		if (_player != null && GodotObject.IsInstanceValid(_player))
		{
			float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);

			// Magnet effect towards player when nearby
			if (distance <= MagnetRange)
			{
				Vector2 direction = GlobalPosition.DirectionTo(_player.GlobalPosition);
				GlobalPosition += direction * MagnetSpeed * dt;
				return;
			}
		}

		// Gentle floating/bobbing effect
		Position = new Vector2(_initialPosition.X, _initialPosition.Y + Mathf.Sin(_bobTimer) * 2.5f);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_collected)
			return;

		if (body is Player player)
		{
			_collected = true;

			if (Type == PickupType.Coin)
			{
				player.CollectMoney(Value);
			}
			else if (Type == PickupType.Health)
			{
				player.Heal(Value);
			}

			// Play pickup audio if attached
			var sfx = GetNodeOrNull<AudioStreamPlayer2D>("PickupSound");
			if (sfx != null)
			{
				sfx.Reparent(GetTree().CurrentScene);
				sfx.Play();
				sfx.Finished += () => sfx.QueueFree();
			}

			QueueFree();
		}
	}
}
