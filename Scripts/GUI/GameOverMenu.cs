using Godot;

public partial class GameOverMenu : CanvasLayer
{
	private Label _titleLabel;
	private Label _summaryLabel;
	private Button _restartButton;
	private Button _menuButton;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_titleLabel = GetNode<Label>("Control/CenterContainer/VBoxContainer/TitleLabel");
		_summaryLabel = GetNode<Label>("Control/CenterContainer/VBoxContainer/SummaryLabel");
		_restartButton = GetNode<Button>("Control/CenterContainer/VBoxContainer/ButtonContainer/RestartButton");
		_menuButton = GetNode<Button>("Control/CenterContainer/VBoxContainer/ButtonContainer/MenuButton");

		_restartButton.Pressed += OnRestartPressed;
		_menuButton.Pressed += OnMenuPressed;
	}

	public void Setup(bool isVictory, int wavesCleared, int maxWaves, int kills, int coins)
	{
		if (_titleLabel == null)
			_titleLabel = GetNode<Label>("Control/CenterContainer/VBoxContainer/TitleLabel");
		if (_summaryLabel == null)
			_summaryLabel = GetNode<Label>("Control/CenterContainer/VBoxContainer/SummaryLabel");

		if (isVictory)
		{
			_titleLabel.Text = "VICTORY!";
			_titleLabel.Modulate = new Color(1.0f, 0.84f, 0.0f); // Gold
		}
		else
		{
			_titleLabel.Text = "YOU DIED";
			_titleLabel.Modulate = new Color(0.9f, 0.2f, 0.2f); // Crimson
		}

		_summaryLabel.Text = 
			$"Waves Cleared: {wavesCleared} / {maxWaves}\n" +
			$"Enemies Slain: {kills}\n" +
			$"Coins Collected: {coins}";
	}

	private void OnRestartPressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://Scenes/game.tscn");
	}

	private void OnMenuPressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://Scenes/Menus/MainMenu.tscn");
	}
}
