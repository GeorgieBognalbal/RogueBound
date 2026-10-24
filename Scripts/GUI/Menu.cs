using Godot;
using System;

public partial class Menu : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GetNode<Button>("CenterContainer/VBoxContainer/PlayButton")
			.Pressed += OnPlayPressed;

		GetNode<Button>("CenterContainer/VBoxContainer/SettingsButton")
			.Pressed += OnSettingsPressed;

		GetNode<Button>("CenterContainer/VBoxContainer/QuitButton")
			.Pressed += OnQuitPressed;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void OnPlayPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/game.tscn");
	}

	private void OnSettingsPressed()
	{
		GD.Print("Settings button clicked!");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
