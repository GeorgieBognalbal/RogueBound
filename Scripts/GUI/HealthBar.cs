
using Godot;

public partial class HealthBar : Control
{
    private Player _player;
    private ProgressBar _progressBar;
    private Label _healthLabel;

    public override void _Ready()
    {
        _progressBar = GetNode<ProgressBar>("ProgressBar");
        _healthLabel = GetNode<Label>("Label");

        _player = GetTree().GetFirstNodeInGroup("player") as Player;

        if (_player == null)
        {
            GD.PushError("HealthBar: Could not find Player!");
            return;
        }

        // Listen for changes in player health.
        _player.HealthChanged += UpdateHealth;

        // Display the initial health.
        UpdateHealth(_player.health, _player.MaxHealth);
    }

    private void UpdateHealth(float currentHealth, float maxHealth)
    {
        _progressBar.MinValue = 0;
        _progressBar.MaxValue = maxHealth;
        _progressBar.Value = currentHealth;

        _healthLabel.Text =
            $"{currentHealth:0} / {maxHealth:0}";
    }

    public override void _ExitTree()
    {
        if (_player != null)
            _player.HealthChanged -= UpdateHealth;
    }
}
