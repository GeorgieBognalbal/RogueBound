using Godot;

public partial class Intro : Control
{
	// Set to true to bypass the cutscene and enter the game immediately.
	public const bool SkipIntro = false;

	private const string GameScenePath = "res://Scenes/game.tscn";
	private const float CharactersPerSecond = 48.0f;
	private const float EscapeHoldSeconds = 1.0f;

	private static readonly string[] IntroCaptions =
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
	private static readonly string[] HappyEndingCaptions =
	{
		"The Orc King falls at last. You break your partner's chains, and she is safe in your arms.",
		"A peaceful morning in the kingdom. The castle stands tall beneath a bright sun, and you live happily ever after."
	};
	private static readonly string[] BadEndingCaptions =
	{
		"Too slow. The Orc King strikes, and your partner falls before your eyes.",
		"You run. You don't stop until the dungeon is far behind you.",
		"You return to the castle, but it has never felt so empty.",
		"You didn't die. You just couldn't save her. And all that's left is the house you promised to build together."
	};

	private TextureRect _frameDisplay;
	private Label _captionLabel;
	private Label _continueLabel;
	private Label _skipHint;
	private ColorRect _fade;
	private Label _endingTitle;
	private Label _endingTitleShadow;
	private ImageTexture _endingTexture;
	private bool _endingMode;
	private bool _happyEnding;
	private float _endingElapsed;
	private float _endingAnimationAccumulator;
	private float _endingTitleAlpha;
	private int _endingAnimationFrame;
	private bool StaticBadEnding => _endingMode && !_happyEnding;

	private int _frameIndex;
	private float _typedCharacters;
	private float _blinkTime;
	private float _escapeHeldTime;
	private bool _typing;
	private bool _escapeDown;
	private bool _transitioning;
	private bool _beginOnRelease;
	private bool _skipOnEscapeRelease;
	private string[] ActiveCaptions => !_endingMode ? IntroCaptions : _happyEnding ? HappyEndingCaptions : BadEndingCaptions;

	public static void PlayEnding(Node parent, bool happyEnding)
	{
		PackedScene introScene = GD.Load<PackedScene>("res://Scenes/Intro.tscn");
		if (introScene == null)
		{
			GD.PushError("The shared intro cutscene scene could not be loaded for the ending.");
			return;
		}
		var layer = new CanvasLayer { Name = "EndingCutsceneLayer", Layer = 100, ProcessMode = Node.ProcessModeEnum.Always };
		Intro sequence = introScene.Instantiate<Intro>();
		sequence._endingMode = true;
		sequence._happyEnding = happyEnding;
		parent.AddChild(layer);
		layer.AddChild(sequence);
	}

	public override void _Ready()
	{
		if (!_endingMode && ShouldSkipIntro())
		{
			GetTree().ChangeSceneToFile(GameScenePath);
			return;
		}

		var storyBgm = new AudioStreamPlayer
		{
			Stream = GD.Load<AudioStream>("res://Sounds/Story BGM.wav"),
			VolumeDb = 0.0f
		};
		AddChild(storyBgm);
		storyBgm.Play();

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
		if (_endingMode && !StaticBadEnding)
		{
			_endingTitleShadow = CreateEndingTitle(true);
			_endingTitle = CreateEndingTitle(false);
			_endingTitleShadow.ZIndex = 10;
			_endingTitle.ZIndex = 11;
			_endingTitleShadow.Visible = false;
			_endingTitle.Visible = false;
			GetTree().Paused = true;
		}
		else if (StaticBadEnding)
		{
			GetNode<Control>("CaptionPanel").Visible = false;
			_skipHint.Visible = false;
			_frameDisplay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			_frameDisplay.StretchMode = TextureRect.StretchModeEnum.Scale;
			GetTree().Paused = true;
		}
		ShowFrame(0);
	}

	private Label CreateEndingTitle(bool shadow)
	{
		string text = _happyEnding ? "Happy Ending" : "B A D   E N D I N G";
		var label = new Label
		{
			Name = shadow ? "EndingTitleShadow" : "EndingTitle",
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorRight = 1.0f,
			OffsetTop = 3.0f,
			OffsetBottom = 52.0f,
			ZIndex = shadow ? 10 : 11,
		};
		label.AddThemeFontOverride("font", GD.Load<Font>("res://Assets/Theme/m5x7.ttf"));
		label.AddThemeFontSizeOverride("font_size", _happyEnding ? 34 : 30);
		label.AddThemeColorOverride("font_color", shadow
			? (_happyEnding ? new Color("#80520b") : new Color("#252a35"))
			: (_happyEnding ? new Color("#ffd56a") : new Color("#aeb5bf")));
		label.AddThemeColorOverride("font_outline_color", shadow ? Colors.Transparent : _happyEnding ? new Color("#d99529") : new Color("#343b49"));
		label.AddThemeConstantOverride("outline_size", shadow ? 0 : _happyEnding ? 7 : 6);
		AddChild(label);
		if (shadow)
			label.Position = new Vector2(0, 3);
		return label;
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
		if (_endingMode && !StaticBadEnding)
		{
			_endingElapsed += elapsed;
			_endingAnimationAccumulator += elapsed;
			float animationStep = !_happyEnding && _frameIndex == 0 ? 1.0f / 9.0f : 1.0f / 6.0f;
			if (_endingAnimationAccumulator >= animationStep)
			{
				_endingAnimationAccumulator -= animationStep;
				_endingAnimationFrame++;
				if (IsAnimatedEndingFrame())
					RefreshEndingFrame();
			}
			if (_endingTitle != null && _endingTitle.Visible && _endingElapsed >= 0.3f)
			{
				_endingTitleAlpha = Mathf.MoveToward(_endingTitleAlpha, 1.0f, elapsed / 0.55f);
				_endingTitle.Modulate = new Color(1, 1, 1, _endingTitleAlpha);
				_endingTitleShadow.Modulate = new Color(1, 1, 1, _endingTitleAlpha);
			}
		}
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
			_captionLabel.VisibleCharacters = Mathf.Min((int)_typedCharacters, ActiveCaptions[_frameIndex].Length);
			if (_captionLabel.VisibleCharacters >= ActiveCaptions[_frameIndex].Length)
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
		if (StaticBadEnding)
		{
			string[] paths =
			{
				"res://Assets/Endings/BadEnding/frame1_arena.png",
				"res://Assets/Endings/BadEnding/frame2_corridor.png",
				"res://Assets/Endings/BadEnding/frame3_castle.png",
				"res://Assets/Endings/BadEnding/frame4_house.png",
			};
			_frameDisplay.Texture = GD.Load<Texture2D>(paths[index]);
			if (_frameDisplay.Texture == null)
				GD.PushError($"Missing bad ending frame: {paths[index]}");
			_typing = false;
		}
		else if (_endingMode)
		{
			_endingElapsed = 0;
			_endingAnimationAccumulator = 0;
			_endingAnimationFrame = 0;
			RefreshEndingFrame();
			bool titleFrame = _happyEnding ? index == 1 : index == 3;
			_endingTitle.Visible = _endingTitleShadow.Visible = titleFrame;
			_endingTitleAlpha = 0;
			_endingTitle.Modulate = _endingTitleShadow.Modulate = new Color(1, 1, 1, 0);
		}
		else
		{
			string framePath = $"res://Assets/Intro/frame{index + 1}.png";
			_frameDisplay.Texture = GD.Load<Texture2D>(framePath);
			if (_frameDisplay.Texture == null)
				GD.PushError($"Missing intro image: {framePath}");
		}

		_captionLabel.Text = ActiveCaptions[index];
		_captionLabel.VisibleCharacters = 0;
		_continueLabel.Text = index == ActiveCaptions.Length - 1
			? (_endingMode ? "Click to finish" : "Click to begin")
			: "Click to continue";
		_continueLabel.Visible = false;
		_typedCharacters = 0.0f;
		_blinkTime = 0.0f;
		_typing = !StaticBadEnding;
	}

	private void AdvanceFromInput()
	{
		if (_transitioning)
			return;
		if (StaticBadEnding)
		{
			if (_frameIndex == ActiveCaptions.Length - 1)
				_beginOnRelease = true;
			else
				_ = TransitionToNextFrame();
			return;
		}

		if (_typing)
		{
			FinishTyping();
			return;
		}

		if (!_continueLabel.Visible)
			return;

		if (_frameIndex == ActiveCaptions.Length - 1)
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
		if (_endingMode)
		{
			GetTree().Paused = false;
			GetTree().ChangeSceneToFile(EndingScene.TITLE_SCENE);
		}
		else GetTree().ChangeSceneToFile(GameScenePath);
	}

	private bool IsAnimatedEndingFrame() => _happyEnding
		? _frameIndex == 0
		: _frameIndex == 0 || _frameIndex == 1 || _frameIndex == 2;

	private void RefreshEndingFrame()
	{
		Image image = GenerateIntroFrames.GenerateEndingFrame(_happyEnding, _frameIndex, _endingAnimationFrame);
		if (_endingTexture == null)
		{
			_endingTexture = ImageTexture.CreateFromImage(image);
			_frameDisplay.Texture = _endingTexture;
		}
		else _endingTexture.Update(image);
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
