using Godot;
using System.Threading.Tasks;

public partial class SceneTransition : CanvasLayer
{
	private ColorRect _fade;
	private bool _transitioning;

	public override void _Ready()
	{
		Layer = 100;
		_fade = new ColorRect
		{
			Color = new Color(0, 0, 0, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = true
		};
		_fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_fade);
	}

	public async Task ChangeSceneWithFade(string scenePath)
	{
		if (_transitioning) return;
		_transitioning = true;
		_fade.MouseFilter = Control.MouseFilterEnum.Stop;
		var fadeIn = CreateTween();
		fadeIn.TweenProperty(_fade, "color:a", 1f, 0.32).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		await ToSignal(fadeIn, Tween.SignalName.Finished);
		Error result = GetTree().ChangeSceneToFile(scenePath);
		if (result == Error.Ok) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		var fadeOut = CreateTween();
		fadeOut.TweenProperty(_fade, "color:a", 0f, 0.42).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		await ToSignal(fadeOut, Tween.SignalName.Finished);
		_fade.MouseFilter = Control.MouseFilterEnum.Ignore;
		_transitioning = false;
	}
}
