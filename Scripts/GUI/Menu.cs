using Godot;

public partial class Menu : Control
{
	[Export] public PackedScene SettingsScene;
	[Export] public PackedScene StatsScene;

	public override void _Ready()
	{
		Button playBtn = GetNodeOrNull<Button>("VBoxContainer/PlayButton") 
			?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/PlayButton");
		Button statsBtn = GetNodeOrNull<Button>("VBoxContainer/StatsButton") 
			?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/StatsButton");
		Button settingsBtn = GetNodeOrNull<Button>("VBoxContainer/SettingsButton") 
			?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/SettingsButton");
		Button quitBtn = GetNodeOrNull<Button>("VBoxContainer/QuitButton") 
			?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/QuitButton");

		if (playBtn != null)
			playBtn.Pressed += OnPlayPressed;
		if (statsBtn != null)
			statsBtn.Pressed += OnStatsPressed;
		if (settingsBtn != null)
			settingsBtn.Pressed += OnSettingsPressed;
		if (quitBtn != null)
			quitBtn.Pressed += OnQuitPressed;
	}

	public void OnPlayPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/game.tscn");
	}

	public void OnStatsPressed()
	{
		var scene = StatsScene ?? GD.Load<PackedScene>("res://Scenes/Menus/StatsMenu.tscn");
		if (scene != null)
		{
			var menu = scene.Instantiate<CanvasLayer>();
			GetTree().Root.AddChild(menu);
		}
	}

	public void OnSettingsPressed()
	{
		var scene = SettingsScene ?? GD.Load<PackedScene>("res://Scenes/Menus/SettingsMenu.tscn");
		if (scene != null)
		{
			var menu = scene.Instantiate<CanvasLayer>();
			GetTree().Root.AddChild(menu);
		}
	}

	public void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
