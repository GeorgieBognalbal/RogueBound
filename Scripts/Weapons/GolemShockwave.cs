using Godot;

public partial class GolemShockwave : Node2D
{
	private float _age;
	private float _radius;
	private Color _color;

	public static void Spawn(Node2D source, float radius, Color color)
	{
		var world = source.GetTree().CurrentScene;
		Vector2 position = source.GlobalPosition;
		Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(world) || world.IsQueuedForDeletion())
				return;
			var effect = new GolemShockwave { Position = position, _radius = radius, _color = color, ZIndex = 8 };
			world.AddChild(effect);
			effect.AddToGroup("floor_items");
		}).CallDeferred();
	}

	public override void _Process(double delta)
	{
		_age += (float)delta;
		if (_age >= 0.45f)
			QueueFree();
		else
			QueueRedraw();
	}

	public override void _Draw()
	{
		float progress = _age / 0.45f;
		Color color = new(_color.R, _color.G, _color.B, 1f - progress);
		float radius = Mathf.Lerp(6f, _radius, progress);
		DrawArc(Vector2.Zero, radius, 0, Mathf.Tau, 40, color, 2f);
		for (int i = 0; i < 8; i++)
		{
			Vector2 direction = Vector2.FromAngle(i * Mathf.Tau / 8);
			DrawLine(direction * radius * 0.65f, direction * radius, color, 2f);
		}
	}
}
