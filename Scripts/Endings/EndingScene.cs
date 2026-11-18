using Godot;

public partial class EndingScene : Control
{
	public const string TITLE_SCENE = "res://Scenes/Menus/MainMenu.tscn";

	[Export] public bool HappyEnding = true;

	public override void _Ready()
	{
		Intro.PlayEnding(this, HappyEnding);
	}
}
