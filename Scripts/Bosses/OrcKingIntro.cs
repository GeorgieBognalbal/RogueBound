using Godot;
using System;

public partial class OrcKingIntro : CanvasLayer
{
	[Signal] public delegate void FinishedEventHandler();
	private string _folder;
	private TextureRect _image;
	private Json _manifest;
	private int _fps = 30, _frame, _introStart, _introEnd, _holdStart, _holdEnd, _outroStart, _outroEnd;
	private float _timer;
	private bool _wasPaused;
	private enum Segment { Intro, Hold, Outro }
	private Segment _segment;

	public static OrcKingIntro Play(Node parent, string folder)
	{
		var layer = new OrcKingIntro { _folder = folder };
		parent.AddChild(layer);
		return layer;
	}

	public override void _Ready()
	{
		Layer = 100;
		ProcessMode = ProcessModeEnum.Always;
		_wasPaused = GetTree().Paused;
		GetTree().Paused = true;
		var introBgm = new AudioStreamPlayer
		{
			Stream = GD.Load<AudioStream>("res://Sounds/Intro Boss BGM.wav"),
			VolumeDb = 0.0f
		};
		AddChild(introBgm);
		introBgm.Play();
		var json = Json.ParseString(FileAccess.GetFileAsString(_folder + "/manifest.json"));
		if (json.VariantType == Variant.Type.Dictionary)
		{
			Godot.Collections.Dictionary data = json.AsGodotDictionary();
			_fps = data.ContainsKey("fps") ? (int)data["fps"] : 30;
			SetRange(data, "intro", out _introStart, out _introEnd);
			SetRange(data, "hold_loop", out _holdStart, out _holdEnd);
			SetRange(data, "outro", out _outroStart, out _outroEnd);
		}
		_image = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore };
		AddChild(_image);
		_image.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_segment = Segment.Intro;
		_frame = _introStart;
		ShowFrame();
	}

	public override void _Process(double delta)
	{
		_timer += (float)delta;
		while (_timer >= 1.0f / Mathf.Max(1, _fps))
		{
			_timer -= 1.0f / Mathf.Max(1, _fps);
			if (_segment == Segment.Intro)
			{
				if (++_frame > _introEnd) { _segment = Segment.Hold; _frame = _holdStart; }
			}
			else if (_segment == Segment.Hold)
			{
				if (++_frame > _holdEnd) { _segment = Segment.Outro; _frame = _outroStart; }
			}
			else if (++_frame > _outroEnd)
			{
				GetTree().Paused = _wasPaused;
				EmitSignal(SignalName.Finished);
				QueueFree();
				return;
			}
			ShowFrame();
		}
	}

	private static void SetRange(Godot.Collections.Dictionary data, string key, out int start, out int end)
	{
		var range = data[key].AsGodotArray();
		start = (int)range[0]; end = (int)range[1];
	}

	private void ShowFrame()
	{
		string file = $"{_folder}/frames_transparent/frame_{_frame:000}.png";
		_image.Texture = GD.Load<Texture2D>(file);
	}

	public override void _ExitTree()
	{
		if (GetTree() != null) GetTree().Paused = _wasPaused;
	}
}
