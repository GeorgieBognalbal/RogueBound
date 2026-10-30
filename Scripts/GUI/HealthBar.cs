
using Godot;

public partial class HealthBar : Control
{
	private float _fullHealthWidth;
	private TextureRect _healthFill;
	private Label _healthLabel;
	private Player _player;

	public override void _Ready()
	{
		_healthFill = GetNode<TextureRect>("HealthFill");
		_healthLabel = GetNode<Label>("HealthLabel");

		_fullHealthWidth = _healthFill.Size.X;

		_player = GetTree().GetFirstNodeInGroup("player") as Player;

		if (_player == null)
		{
			GD.PushWarning("HealthBar: Player not found.");
			return;
		}

		_player.HealthChanged += UpdateHealth;
		UpdateHealth(_player.health, _player.MaxHealth);
	}

	private void UpdateHealth(float current, float maximum)
	{
		_healthLabel.Text = $"{current:0} / {maximum:0}";

		float ratio = maximum > 0
			? Mathf.Clamp(current / maximum, 0.0f, 1.0f)
			: 0.0f;

		_healthFill.Size = new Vector2(
			_fullHealthWidth * ratio,
			_healthFill.Size.Y
		);
	}

	public override void _ExitTree()
	{
		if (_player != null)
			_player.HealthChanged -= UpdateHealth;
	}
}
