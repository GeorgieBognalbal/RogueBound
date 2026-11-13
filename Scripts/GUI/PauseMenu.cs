using Godot;

public partial class PauseMenu : CanvasLayer
{
	[Export] public PackedScene SettingsScene;

	private Control _pausePanel;
	private Button _resumeButton;
	private Button _restartButton;
	private Button _settingsButton;
	private Button _menuButton;

	private bool _isPaused = false;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_pausePanel = GetNode<Control>("PauseControl");
		_resumeButton = GetNode<Button>("PauseControl/CenterContainer/VBoxContainer/ResumeButton");
		_restartButton = GetNode<Button>("PauseControl/CenterContainer/VBoxContainer/RestartButton");
		_settingsButton = GetNode<Button>("PauseControl/CenterContainer/VBoxContainer/SettingsButton");
		_menuButton = GetNode<Button>("PauseControl/CenterContainer/VBoxContainer/MenuButton");

		_resumeButton.Pressed += ResumeGame;
		_restartButton.Pressed += RestartGame;
		_settingsButton.Pressed += OpenSettings;
		_menuButton.Pressed += QuitToMenu;

		_pausePanel.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			TogglePause();
			GetViewport().SetInputAsHandled();
		}
	}

	public void TogglePause()
	{
		_isPaused = !_isPaused;
		_pausePanel.Visible = _isPaused;
		GetTree().Paused = _isPaused;
	}

	public void ResumeGame()
	{
		_isPaused = false;
		_pausePanel.Visible = false;
		GetTree().Paused = false;
	}

	private void RestartGame()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://Scenes/game.tscn");
	}

	private void OpenSettings()
	{
		var settings = SettingsScene ?? GD.Load<PackedScene>("res://Scenes/Menus/SettingsMenu.tscn");
		if (settings != null)
		{
			var menu = settings.Instantiate<CanvasLayer>();
			AddChild(menu);
		}
	}

	private void QuitToMenu()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://Scenes/Menus/MainMenu.tscn");
	}
}
