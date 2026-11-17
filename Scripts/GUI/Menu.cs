using Godot;

public partial class Menu : Control
{
	[Export] public PackedScene SettingsScene;

	public override void _Ready()
	{
		foreach (Node node in GetTree().GetNodesInGroup("menu_buttons"))
			if (node is Button button) AddHoverAnimation(button);
		var container = GetNodeOrNull<VBoxContainer>("VBoxContainer");
		if (container != null)
			foreach (Node node in container.GetChildren())
				if (node is Button button) AddHoverAnimation(button);
	}

	private void AddHoverAnimation(Control button)
	{
		button.PivotOffset = button.Size / 2f;
		button.Resized += () => button.PivotOffset = button.Size / 2f;
		button.MouseEntered += () => AnimateButton(button, new Vector2(1.06f, 1.06f));
		button.MouseExited += () => AnimateButton(button, Vector2.One);
	}

	private void AnimateButton(Control button, Vector2 target)
	{
		var tween = CreateTween();
		tween.TweenProperty(button, "scale", target, 0.16)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	public void OnPlayPressed()
	{
		// Keep save selection intact; a Start-intent selection enters the intro from SaveFilesMenu.
		OpenSaveFiles(true);
	}

	public void OnStatsPressed()
	{
		OpenSaveFiles(false);
	}

	private void OpenSaveFiles(bool playIntent)
	{
		var scene = GD.Load<PackedScene>("res://Scenes/Menus/SaveFilesMenu.tscn");
		if (scene == null)
		{
			GD.PushError("SaveFilesMenu.tscn could not be loaded.");
			return;
		}

		var menu = scene.Instantiate<SaveFilesMenu>();
		menu.PlayIntent = playIntent;
		GetTree().CurrentScene.AddChild(menu);
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
