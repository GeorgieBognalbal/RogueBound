using Godot;

public partial class SettingsMenu : CanvasLayer
{
	private HSlider _volumeSlider;
	private CheckBox _fullscreenCheck;
	private Button _closeButton;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_volumeSlider = GetNode<HSlider>("Control/CenterContainer/VBoxContainer/VolumeRow/VolumeSlider");
		_fullscreenCheck = GetNode<CheckBox>("Control/CenterContainer/VBoxContainer/FullscreenCheck");
		_closeButton = GetNode<Button>("Control/CenterContainer/VBoxContainer/CloseButton");

		int masterBus = AudioServer.GetBusIndex("Master");
		float currentDb = AudioServer.GetBusVolumeDb(masterBus);
		_volumeSlider.Value = Mathf.DbToLinear(currentDb);

		_fullscreenCheck.ButtonPressed = 
			DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;

		_volumeSlider.ValueChanged += OnVolumeChanged;
		_fullscreenCheck.Toggled += OnFullscreenToggled;
		_closeButton.Pressed += () => QueueFree();
	}

	private void OnVolumeChanged(double value)
	{
		int masterBus = AudioServer.GetBusIndex("Master");
		float db = (float)Mathf.LinearToDb(value);
		if (value <= 0.001)
			db = -80.0f; // Mute
		AudioServer.SetBusVolumeDb(masterBus, db);
	}

	private void OnFullscreenToggled(bool toggledOn)
	{
		DisplayServer.WindowSetMode(
			toggledOn ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed
		);
	}
}
