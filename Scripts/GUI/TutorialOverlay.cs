using Godot;

public partial class TutorialOverlay : CanvasLayer
{
	private const string SettingsPath = "user://roguebound_settings.cfg";

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		GetNode<Button>("Control/CenterPanel/Margins/Content/ContinueButton").Pressed += OnContinuePressed;
	}

	private void OnContinuePressed()
	{
		var settings = new ConfigFile();
		settings.Load(SettingsPath);
		settings.SetValue("tutorial", "tutorial_done", true);
		Error result = settings.Save(SettingsPath);
		if (result != Error.Ok)
			GD.PushWarning($"Could not save tutorial_done setting: {result}");
		QueueFree();
	}
}
