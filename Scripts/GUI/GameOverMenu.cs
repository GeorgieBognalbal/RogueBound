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

		_titleLabel = GetNodeOrNull<Label>("Control/CenterContainer/VBoxContainer/TitleLabel");
		_summaryLabel = GetNodeOrNull<Label>("Control/CenterContainer/VBoxContainer/SummaryLabel");
		_restartButton = GetNodeOrNull<Button>("Control/CenterContainer/VBoxContainer/ButtonContainer/RestartButton");
		_menuButton = GetNodeOrNull<Button>("Control/CenterContainer/VBoxContainer/ButtonContainer/MenuButton");

		if (_restartButton != null)
			_restartButton.Pressed += OnRestartPressed;

		if (_menuButton != null)
			_menuButton.Pressed += OnMenuPressed;

		// Grab focus so keyboard / spacebar works immediately
		_restartButton?.GrabFocus();

		// Hide in-game HUD underneath to keep game over clean
		HideGameHud();
	}

	private void HideGameHud()
	{
		var player = GetTree().GetFirstNodeInGroup("player") as Node;
		var playerHud = player?.GetNodeOrNull<CanvasLayer>("HUD");
		if (playerHud != null)
		{
			playerHud.Visible = false;
		}

		var sceneHud = GetTree().CurrentScene?.GetNodeOrNull<CanvasLayer>("HUD");
		if (sceneHud != null)
		{
			sceneHud.Visible = false;
		}
	}

	public void Setup(bool isVictory, int wavesCleared, int maxWaves, int kills, int coins)
	{
		if (_titleLabel == null)
			_titleLabel = GetNodeOrNull<Label>("Control/CenterContainer/VBoxContainer/TitleLabel");
		if (_summaryLabel == null)
			_summaryLabel = GetNodeOrNull<Label>("Control/CenterContainer/VBoxContainer/SummaryLabel");

		if (_titleLabel != null)
		{
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
		}

		if (_summaryLabel != null)
		{
			_summaryLabel.Text = 
				$"Rooms / Waves Cleared: {wavesCleared} / {maxWaves}\n" +
				$"Enemies Slain: {kills}\n" +
				$"Coins Collected: {coins}";
		}
	}

	private void OnRestartPressed()
	{
		GetTree().Paused = false;
		QueueFree();
		GetTree().ChangeSceneToFile("res://Scenes/game.tscn");
	}

	private void OnMenuPressed()
	{
		GetTree().Paused = false;
		QueueFree();
		GetTree().ChangeSceneToFile("res://Scenes/Menus/MainMenu.tscn");
	}
}
