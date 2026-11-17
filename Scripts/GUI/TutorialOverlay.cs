using Godot;
using System;

public partial class TutorialOverlay : CanvasLayer
{
	public event Action Continued;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		GetNode<Button>("Control/CenterPanel/Margins/Content/ContinueButton").Pressed += OnContinuePressed;
	}

	private void OnContinuePressed()
	{
		Continued?.Invoke();
		QueueFree();
	}
}
