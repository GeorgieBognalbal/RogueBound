using Godot;

public partial class Intro : Control
{
	// Set to true to bypass the cutscene and enter the game immediately.
	public const bool SkipIntro = false;

	private const string GameScenePath = "res://Scenes/game.tscn";
	private const float CharactersPerSecond = 48.0f;
	private const float EscapeHoldSeconds = 1.0f;

	private static readonly string[] Captions =
	{
		"A peaceful morning in the kingdom. The castle stands tall beneath a bright sun, and its people live in peace.",
		"Inside its walls, the knight and his beloved partner share a rare, happy moment. Nothing could go wrong.",
		"But night falls, and with it comes a darkness no one expected.",
		"Enemies attack without warning. Towers burn, and the castle is swallowed by flames.",
		"Through the smoke and chaos, the knight runs to find his partner.",
		"He is too late. The invaders have seized his partner and are dragging them away.",
		"The enemies vanish into the entrance of a dark dungeon beneath the ruins.",
		"The knight grips his weapon and follows them down. He will not return without his partner."
	};

	private TextureRect _frameDisplay;
	private Label _captionLabel;
	private Label _continueLabel;
	private Label _skipHint;
	private ColorRect _fade;

	private int _frameIndex;
	private float _typedCharacters;
	private float _blinkTime;
	private float _escapeHeldTime;
	private bool _typing;
	private bool _escapeDown;
	private bool _transitioning;
	private bool _beginOnRelease;
	private bool _skipOnEscapeRelease;

	public override void _Ready()
	{
		if (ShouldSkipIntro())
		{
			GetTree().ChangeSceneToFile(GameScenePath);
			return;
		}

		_frameDisplay = GetNode<TextureRect>("Frame");
		_captionLabel = GetNode<Label>("CaptionPanel/Caption");
		_continueLabel = GetNode<Label>("CaptionPanel/ContinuePrompt");
		_skipHint = GetNode<Label>("SkipHint");
		_fade = GetNode<ColorRect>("Fade");

		TextureFilter = TextureFilterEnum.Nearest;
		_frameDisplay.TextureFilter = TextureFilterEnum.Nearest;
		_frameDisplay.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_fade.MouseFilter = MouseFilterEnum.Ignore;
		_continueLabel.Visible = false;
		ShowFrame(0);
	}

	private static bool ShouldSkipIntro() => SkipIntro;

	public override void _Input(InputEvent @event)
	{
		if (_frameDisplay == null)
			return;

		if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			if (mouseEvent.Pressed)
				AdvanceFromInput();
			else if (_beginOnRelease)
				LoadGame();
			GetViewport()?.SetInputAsHandled();
			return;
		}

		if (@event is InputEventKey keyEvent)
		{
			if (keyEvent.Keycode == Key.Escape)
			{
				_escapeDown = keyEvent.Pressed;
				if (!keyEvent.Pressed)
				{
					if (_skipOnEscapeRelease)
						LoadGame();
					_skipOnEscapeRelease = false;
					_escapeHeldTime = 0.0f;
				}
				GetViewport()?.SetInputAsHandled();
				return;
			}

			bool advanceKey = keyEvent.Keycode == Key.Space ||
				keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.KpEnter;
			if (advanceKey)
			{
				if (keyEvent.Pressed && !keyEvent.Echo)
					AdvanceFromInput();
				else if (!keyEvent.Pressed && _beginOnRelease)
					LoadGame();
				GetViewport()?.SetInputAsHandled();
			}
		}
	}

	public override void _Process(double delta)
	{
		float elapsed = (float)delta;
		if (_escapeDown)
		{
			_escapeHeldTime += elapsed;
			float holdProgress = Mathf.Clamp(_escapeHeldTime / EscapeHoldSeconds, 0.0f, 1.0f);
			_skipHint.Modulate = new Color(1.0f, 1.0f, 1.0f, Mathf.Lerp(0.72f, 1.0f, holdProgress));
			if (_escapeHeldTime >= EscapeHoldSeconds)
				_skipOnEscapeRelease = true;
		}
		else
		{
			_skipHint.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.86f);
		}

		if (!_skipOnEscapeRelease && _typing)
		{
			_typedCharacters += CharactersPerSecond * elapsed;
			_captionLabel.VisibleCharacters = Mathf.Min((int)_typedCharacters, Captions[_frameIndex].Length);
			if (_captionLabel.VisibleCharacters >= Captions[_frameIndex].Length)
				FinishTyping();
		}
		else if (_continueLabel.Visible)
		{
			_blinkTime += elapsed;
			float alpha = 0.58f + 0.42f * (0.5f + 0.5f * Mathf.Sin(_blinkTime * Mathf.Pi));
			_continueLabel.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha);
		}
	}

	private void ShowFrame(int index)
	{
		_frameIndex = index;
		string framePath = $"res://Assets/Intro/frame{index + 1}.png";
		_frameDisplay.Texture = GD.Load<Texture2D>(framePath);
		if (_frameDisplay.Texture == null)
			GD.PushError($"Missing intro image: {framePath}");

		_captionLabel.Text = Captions[index];
		_captionLabel.VisibleCharacters = 0;
		_continueLabel.Text = index == Captions.Length - 1 ? "Click to begin" : "Click to continue";
		_continueLabel.Visible = false;
		_typedCharacters = 0.0f;
		_blinkTime = 0.0f;
		_typing = true;
	}

	private void AdvanceFromInput()
	{
		if (_transitioning)
			return;

		if (_typing)
		{
			FinishTyping();
			return;
		}

		if (!_continueLabel.Visible)
			return;

		if (_frameIndex == Captions.Length - 1)
		{
			_beginOnRelease = true;
			return;
		}

		_ = TransitionToNextFrame();
	}

	private void FinishTyping()
	{
		_typing = false;
		_captionLabel.VisibleCharacters = -1;
		_continueLabel.Visible = true;
		_blinkTime = 0.0f;
	}

	private void LoadGame()
	{
		_beginOnRelease = false;
		_skipOnEscapeRelease = false;
		GetTree().ChangeSceneToFile(GameScenePath);
	}

	private async System.Threading.Tasks.Task TransitionToNextFrame()
	{
		_transitioning = true;
		Tween fadeOut = CreateTween();
		fadeOut.TweenProperty(_fade, "color:a", 1.0f, 0.14f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		await ToSignal(fadeOut, Tween.SignalName.Finished);

		ShowFrame(_frameIndex + 1);
		Tween fadeIn = CreateTween();
		fadeIn.TweenProperty(_fade, "color:a", 0.0f, 0.18f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		await ToSignal(fadeIn, Tween.SignalName.Finished);
		_transitioning = false;
	}
}
