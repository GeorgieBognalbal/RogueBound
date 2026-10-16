
using Godot;

public partial class Enemy : CharacterBody2D
{
	[Export] public float Speed = 50.0f;
	[Export] public float StopDistance = 20.0f;
	[Export] public float RepathInterval = 0.2f;

	private Node2D _player;
	private NavigationAgent2D _navAgent;

	private float _repathTimer;

	public override async void _Ready()
	{
		// Find the player
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		// Get the NavigationAgent2D child
		_navAgent = GetNode<NavigationAgent2D>("EnemyNavigationAgent");

		_navAgent.PathDesiredDistance = 4.0f;
		_navAgent.TargetDesiredDistance = StopDistance;

		// Wait for the navigation map to synchronize
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		if (_player != null)
		{
			_navAgent.TargetPosition = _player.GlobalPosition;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_player == null)
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		// Update the target periodically
		_repathTimer -= (float)delta;

		if (_repathTimer <= 0.0f)
		{
			_navAgent.TargetPosition = _player.GlobalPosition;
			_repathTimer = RepathInterval;
		}

/*		
 *		// Note: 
 *			-	di need kase dapat laging hinahabol player (maybe sa ibang enemy na dumidikit sa player or somethin)

		// Stop if close enough to the player
		if (GlobalPosition.DistanceTo(_player.GlobalPosition) <= StopDistance)
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

		// Stop if navigation has finished or no movement is needed
		if (_navAgent.IsNavigationFinished())
		{
			Velocity = Vector2.Zero;
			MoveAndSlide();
			return;
		}

 */

		// Get the next point along the navigation path
		Vector2 nextPoint = _navAgent.GetNextPathPosition();

		Vector2 direction = GlobalPosition.DirectionTo(nextPoint);

		Velocity = direction * Speed;

		// Move with physical collision detection
		MoveAndSlide();
	}
}
