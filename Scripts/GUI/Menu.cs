using Godot;

public partial class Menu : Control
{
	[Export] public PackedScene SettingsScene;
	[Export] public PackedScene StatsScene;

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
