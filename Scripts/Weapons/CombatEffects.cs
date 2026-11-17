using Godot;

public static class CombatEffects
{
	public static readonly Color Magic = new(0.43f, 0.77f, 1f);
	public static readonly Color Shield = new(0.45f, 1f, 0.85f);
	public static readonly Color Impact = new(1f, 0.55f, 0.28f);

	public static void Burst(Node2D source, Color color, Vector2 offset = default, Vector2 direction = default,
		int amount = 9, Texture2D texture = null)
	{
		var world = source.GetTree().CurrentScene;
		if (world == null)
			return;

		Vector2 position = source.GlobalPosition + offset;
		// Hit callbacks can run while the physics server is flushing contacts.
		Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(world) || !world.IsInsideTree() || world.IsQueuedForDeletion())
				return;

			var particles = new CpuParticles2D
			{
				Emitting = false,
				OneShot = true,
				Amount = amount,
				Texture = texture,
				Lifetime = texture == null ? 0.26 : 0.4,
				Explosiveness = 1f,
				Direction = direction,
				Spread = direction == Vector2.Zero ? 180f : 55f,
				Gravity = texture == null ? new Vector2(0, 40f) : Vector2.Zero,
				InitialVelocityMin = texture == null ? 22f : 80f,
				InitialVelocityMax = texture == null ? 65f : 170f,
				ScaleAmountMin = texture == null ? 1f : 0.2f,
				ScaleAmountMax = texture == null ? 2.5f : 0.35f,
				Color = color,
				ZIndex = 15
			};
			particles.Finished += particles.QueueFree;
			world.AddChild(particles);
			particles.GlobalPosition = position;
			particles.Emitting = true;
		}).CallDeferred();
	}
}
