
using Godot;

public partial class DamageNumber : Node2D
{
	private Label _label;

	public override void _Ready()
	{
		_label = GetNode<Label>("Label");
	}

	public void ShowDamage(float damage)
	{
		_label.Text = $"{damage:0.#}";

		// Randomize the starting position slightly.
		Position += new Vector2(
			(float)GD.RandRange(-8.0, 8.0),
			(float)GD.RandRange(-4.0, 4.0)
		);

		// Float upward and fade out.
		Tween tween = CreateTween();
		tween.SetParallel(true);

		tween.TweenProperty(
			this, "position:y",
			Position.Y - 35.0f, 0.65
		).SetEase(Tween.EaseType.Out);

		tween.TweenProperty(
			_label, "modulate:a",
			0.0f, 0.65
		).SetEase(Tween.EaseType.In);

		tween.Chain().TweenCallback(
			Callable.From(QueueFree)
		);
	}
}
