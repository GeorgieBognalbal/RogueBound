using Godot;

/// <summary>Brief radial lightning effect used by the charged staff attack.</summary>
public partial class LightningBurst : Node2D
{
	[Export] public float Radius = 115.0f;
	[Export] public float Duration = 0.28f;
	private float _remaining;

	public override void _Ready()
	{
		_remaining = Duration;
		ZIndex = 20;
	}

	public override void _Process(double delta)
	{
		_remaining -= (float)delta;
		if (_remaining <= 0.0f)
			QueueFree();
		else
			QueueRedraw();
	}

	public override void _Draw()
	{
		float alpha = Mathf.Clamp(_remaining / Mathf.Max(Duration, 0.01f), 0.0f, 1.0f);
		Color glow = new Color(0.25f, 0.72f, 1.0f, alpha * 0.35f);
		Color bolt = new Color(0.68f, 0.92f, 1.0f, alpha);
		DrawArc(Vector2.Zero, Radius, 0.0f, Mathf.Tau, 48, glow, 5.0f, true);
		DrawArc(Vector2.Zero, Radius, 0.0f, Mathf.Tau, 48, bolt, 1.5f, true);

		for (int i = 0; i < 10; i++)
		{
			float angle = Mathf.Tau * i / 10.0f + (float)GD.RandRange(-0.16, 0.16);
			Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			Vector2 tangent = direction.Orthogonal();
			var points = new Vector2[6];
			for (int j = 0; j < points.Length; j++)
			{
				float distance = Radius * j / (points.Length - 1.0f);
				float offset = j == 0 || j == points.Length - 1
					? 0.0f
					: (float)GD.RandRange(-8.0, 8.0);
				points[j] = direction * distance + tangent * offset;
			}
			DrawPolyline(points, glow, 4.0f, true);
			DrawPolyline(points, bolt, 1.5f, true);
		}
	}
}
