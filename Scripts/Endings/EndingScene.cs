using Godot;

public partial class EndingScene : Control
{
	public const string VICTORY_ENDING = "res://Scenes/Endings/VictoryEnding.tscn";
	public const string DEFEAT_ENDING = "res://Scenes/Endings/DefeatEnding.tscn";
	public const string TITLE_SCENE = "res://Scenes/Menus/MainMenu.tscn";

	[Export] public string Heading = "ENDING";
	[Export(PropertyHint.MultilineText)] public string Caption = "Placeholder ending text.";

	private bool _leaving;

	public override void _Ready()
	{
		TextureFilter = TextureFilterEnum.Nearest;
		var title = GetNode<Label>("CaptionPanel/Heading");
		var body = GetNode<Label>("CaptionPanel/Body");
		title.Text = Heading;
		body.Text = Caption;
		Font font = GD.Load<Font>("res://Assets/Theme/m5x7.ttf");
		foreach (Label label in new[] { title, body, GetNode<Label>("CaptionPanel/ContinuePrompt") })
			if (font != null)
				label.AddThemeFontOverride("font", font);
	}

	public override void _Input(InputEvent input)
	{
		bool pressed = input is InputEventMouseButton mouse && mouse.Pressed ||
			input is InputEventKey key && key.Pressed && !key.Echo &&
			(key.Keycode == Key.Space || key.Keycode == Key.Enter || key.Keycode == Key.KpEnter);
		if (!pressed)
			return;
		GetViewport().SetInputAsHandled();
		if (_leaving)
			return;
		_leaving = true;
		GetTree().ChangeSceneToFile(TITLE_SCENE);
	}
}
