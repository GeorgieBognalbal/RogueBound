using Godot;
using System;

public partial class TutorialOverlay : CanvasLayer
{
	public event Action Continued;
	private bool _showingFinalStep;
	private Label _title;
	private Label _instructions;
	private Label _finalStep;
	private Button _continueButton;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		_title = GetNode<Label>("Control/CenterPanel/Margins/Content/Title");
		_instructions = GetNode<Label>("Control/CenterPanel/Margins/Content/Instructions");
		_finalStep = GetNode<Label>("Control/CenterPanel/Margins/Content/FinalStep");
		_continueButton = GetNode<Button>("Control/CenterPanel/Margins/Content/ContinueButton");
		_continueButton.Pressed += OnContinuePressed;
	}

	private void OnContinuePressed()
	{
		if (!_showingFinalStep)
		{
			_showingFinalStep = true;
			_title.Text = "FINAL STEP";
			_instructions.Hide();
			_finalStep.Show();
			_continueButton.Text = "Finish";
			return;
		}

		Continued?.Invoke();
		QueueFree();
	}
}
