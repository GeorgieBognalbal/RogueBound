using Godot;

// <summary>
// Fighting-game style boss intro: screen greys out, letterbox bars close in,
// a tilted color band slides across, the boss pops in big, and the name appears.
// Everything is built in code, so there is no .tscn to set up.
//
// Usage (from the boss script or the room that triggers the fight):
//   var intro = BossIntro.Play(this, GD.Load<Texture2D>("res://Assets/Bosses/galio_intro_sprite.png"), "GALIO", "The Golem");
//   await ToSignal(intro, BossIntro.SignalName.Finished);
//   // ...start the fight / show the boss HP bar here
// </summary>
public partial class BossIntro : CanvasLayer
{
    [Signal] public delegate void FinishedEventHandler();

    [Export] public Texture2D BossTexture;
    [Export] public string BossName = "GALIO";
    [Export] public string Subtitle = "The Golem";
    [Export] public Font TextFont;                       // leave empty to use the project's default font
    [Export] public Color BandColor = new Color("a8541c");
    [Export] public Color BandEdgeColor = new Color("ec963c");
    [Export] public Color SubtitleColor = new Color("ffd696");
    [Export] public float BandAngleDegrees = 10f;
    [Export] public float HoldSeconds = 2.0f;
    [Export] public int TitleFontSize = 0;               // 0 = auto (scales with screen height)
    [Export] public int SubtitleFontSize = 0;            // 0 = auto

    private const string DimShader = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_nearest;
uniform float amount : hint_range(0.0, 1.0) = 0.0;
void fragment() {
    vec3 c = texture(screen_tex, SCREEN_UV).rgb;
    float g = dot(c, vec3(0.299, 0.587, 0.114));
    COLOR = vec4(mix(c, vec3(g) * 0.5, amount), 1.0);
}";

    private enum State { In, Hold, Out }
    private State _state = State.In;

    private Control _root, _bandPivot, _textHolder;
    private TextureRect _boss;
    private ColorRect _topBar, _bottomBar;
    private ShaderMaterial _dimMat;
    private Vector2 _bossRest;
    private float _w, _h, _barH, _holdLeft;
    private bool _wasPaused, _skip;

    public static BossIntro Play(Node parent, Texture2D boss, string name = "GALIO",
                                 string subtitle = "The Golem", Font font = null,
                                 Color? band = null, Color? edge = null, Color? subtitleColor = null)
    {
        var intro = new BossIntro { BossTexture = boss, BossName = name, Subtitle = subtitle, TextFont = font };
        if (band.HasValue) intro.BandColor = band.Value;
        if (edge.HasValue) intro.BandEdgeColor = edge.Value;
        if (subtitleColor.HasValue) intro.SubtitleColor = subtitleColor.Value;
        parent.AddChild(intro);
        return intro;
    }

    public override void _Ready()
    {
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;   // keep animating while the game is paused
        _wasPaused = GetTree().Paused;
        GetTree().Paused = true;
        var introBgm = new AudioStreamPlayer
        {
            Stream = GD.Load<AudioStream>("res://Sounds/Intro Boss BGM.wav"),
            VolumeDb = 0.0f
        };
        AddChild(introBgm);
        introBgm.Play();
        Build();
        PlayIn();
    }

    public override void _Process(double delta)
    {
        if (_state != State.Hold) return;
        _holdLeft -= (float)delta;
        if (_holdLeft <= 0f || _skip) PlayOut();
    }

    public override void _Input(InputEvent e)
    {
        bool pressed = (e is InputEventKey k && k.Pressed && !k.Echo)
                    || (e is InputEventMouseButton m && m.Pressed)
                    || (e is InputEventJoypadButton j && j.Pressed);
        if (!pressed) return;

        GetViewport().SetInputAsHandled();      // the skip click never reaches the game
        if (_state == State.Hold && _holdLeft < HoldSeconds - 0.4f) _skip = true;
    }

    // ---------------------------------------------------------------- build

    private void Build()
    {
        var size = GetViewport().GetVisibleRect().Size;
        _w = size.X;
        _h = size.Y;
        float u = _h / 180f;                    // layout unit: 1u = 1 px on a 320x180 canvas
        _barH = _h * 0.12f;

        _root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ClipContents = true
        };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // 1. grey-out layer
        _dimMat = new ShaderMaterial { Shader = new Shader { Code = DimShader } };
        _dimMat.SetShaderParameter("amount", 0f);
        var dim = new ColorRect { Color = Colors.White, Material = _dimMat, MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // 2. tilted band (pivot sits at screen center and rotates everything inside it)
        float bandH = _h * 0.52f;
        float bandW = _w * 2.4f;
        float edge = Mathf.Max(2f, 1.5f * u);
        _bandPivot = new Control
        {
            Position = new Vector2(_w / 2f - _w * 1.4f, _h / 2f),   // starts off-screen left
            RotationDegrees = BandAngleDegrees,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.AddChild(_bandPivot);
        AddRect(_bandPivot, new Vector2(-bandW / 2f, -bandH / 2f), new Vector2(bandW, bandH), BandColor);
        var stripe = BandColor.Darkened(0.07f);
        for (float x = -bandW / 2f; x < bandW / 2f; x += 24f * u)
            AddRect(_bandPivot, new Vector2(x, -bandH / 2f + edge), new Vector2(7f * u, bandH - edge * 2f), stripe);
        AddRect(_bandPivot, new Vector2(-bandW / 2f, -bandH / 2f), new Vector2(bandW, edge), BandEdgeColor);
        AddRect(_bandPivot, new Vector2(-bandW / 2f, bandH / 2f - edge), new Vector2(bandW, edge), BandEdgeColor);

        // 3. name + subtitle (inside the band so they tilt with it)
        _textHolder = new Control
        {
            Position = new Vector2(-_w * 0.6f, 0f),
            Modulate = new Color(1, 1, 1, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _bandPivot.AddChild(_textHolder);

        int titleSize = TitleFontSize > 0 ? TitleFontSize : Mathf.RoundToInt(_h * 0.17f);
        int subSize = SubtitleFontSize > 0 ? SubtitleFontSize : Mathf.RoundToInt(_h * 0.095f);
        float textX = -_w * 0.44f;
        float titleY = -_h * 0.19f;

        var title = MakeLabel(BossName, titleSize, Colors.White);
        title.Position = new Vector2(textX, titleY);
        _textHolder.AddChild(title);

        var sub = MakeLabel(Subtitle, subSize, SubtitleColor);
        sub.Position = new Vector2(textX, titleY + titleSize * 1.15f);
        _textHolder.AddChild(sub);

        // 4. boss portrait, big, integer-scaled, rising above the band
        if (BossTexture != null)
        {
            float scale = Mathf.Max(1f, Mathf.Floor(_h * 0.8f / BossTexture.GetHeight()));
            var bs = new Vector2(BossTexture.GetWidth(), BossTexture.GetHeight()) * scale;
            _bossRest = new Vector2(_w * 0.78f - bs.X / 2f, _h * 0.17f);
            _boss = new TextureRect
            {
                Texture = BossTexture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Size = bs,
                Position = _bossRest + new Vector2(_w * 0.6f, 0f)       // starts off-screen right
            };
            _root.AddChild(_boss);
        }
        else
        {
            GD.PushWarning("BossIntro: no BossTexture was given.");
        }

        // 5. letterbox bars on top of everything
        var barColor = new Color(0.04f, 0.04f, 0.05f);
        _topBar = AddRect(_root, new Vector2(0f, -_barH), new Vector2(_w, _barH), barColor);
        _bottomBar = AddRect(_root, new Vector2(0f, _h), new Vector2(_w, _barH), barColor);
    }

    private Label MakeLabel(string text, int size, Color color)
    {
        var settings = new LabelSettings
        {
            FontSize = size,
            FontColor = color,
            OutlineSize = Mathf.Max(4, size / 7),
            OutlineColor = Colors.Black
        };
        if (TextFont != null) settings.Font = TextFont;
        return new Label { Text = text, LabelSettings = settings, MouseFilter = Control.MouseFilterEnum.Ignore };
    }

    private static ColorRect AddRect(Control parent, Vector2 pos, Vector2 size, Color color)
    {
        var r = new ColorRect { Position = pos, Size = size, Color = color, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(r);
        return r;
    }

    // ------------------------------------------------------------ animation

    private Tween NewTween()
    {
        var t = CreateTween();
        t.SetPauseMode(Tween.TweenPauseMode.Process);   // run while the tree is paused
        t.SetParallel(true);
        return t;
    }

    private void PlayIn()
    {
        var t = NewTween();
        t.TweenProperty(_dimMat, "shader_parameter/amount", 1f, 0.3f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

        t.TweenProperty(_topBar, "position:y", 0f, 0.3f).SetDelay(0.15)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenProperty(_bottomBar, "position:y", _h - _barH, 0.3f).SetDelay(0.15)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        t.TweenProperty(_bandPivot, "position:x", _w / 2f, 0.5f).SetDelay(0.35)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        if (_boss != null)
            t.TweenProperty(_boss, "position:x", _bossRest.X, 0.5f).SetDelay(0.55)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        t.TweenProperty(_textHolder, "position:x", 0f, 0.4f).SetDelay(0.95)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenProperty(_textHolder, "modulate:a", 1f, 0.3f).SetDelay(0.95);

        t.Finished += () =>
        {
            _holdLeft = HoldSeconds;
            _state = State.Hold;
        };
    }

    private void PlayOut()
    {
        _state = State.Out;
        var t = NewTween();

        t.TweenProperty(_textHolder, "modulate:a", 0f, 0.2f);

        if (_boss != null)
            t.TweenProperty(_boss, "position:x", _bossRest.X + _w * 0.6f, 0.35f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);

        t.TweenProperty(_bandPivot, "position:x", _w / 2f - _w * 1.4f, 0.4f).SetDelay(0.1)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);

        t.TweenProperty(_topBar, "position:y", -_barH, 0.25f).SetDelay(0.3)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        t.TweenProperty(_bottomBar, "position:y", _h, 0.25f).SetDelay(0.3)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);

        t.TweenProperty(_dimMat, "shader_parameter/amount", 0f, 0.3f).SetDelay(0.3);

        t.Finished += () =>
        {
            GetTree().Paused = _wasPaused;
            EmitSignal(SignalName.Finished);
            QueueFree();
        };
    }
}
