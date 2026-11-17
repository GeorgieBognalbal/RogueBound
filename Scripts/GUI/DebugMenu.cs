using Godot;
using RogueBound.Dungeon;

public partial class DebugMenu : CanvasLayer
{
	private Control _backdrop;
	private Player _player;
	private DungeonManager _dungeon;
	private CheckButton _godMode;
	private SpinBox _floor;
	private Button _go;
	private Button _next;
	private bool _wasPaused;

	public override void _Ready()
	{
		_player = GetParent<Player>();
		_dungeon = GetTree().CurrentScene.GetNodeOrNull<DungeonManager>("DungeonManager");
		_backdrop = GetNode<Control>("Backdrop");
		_godMode = GetNode<CheckButton>("%GodMode");
		_floor = GetNode<SpinBox>("%Floor");
		_go = GetNode<Button>("%Go");
		_next = GetNode<Button>("%Next");
		_godMode.Toggled += enabled =>
		{
			_player.DebugGodMode = enabled;
		};
		GetNode<Button>("%Heal").Pressed += () => _player.Heal(_player.MaxHealth);
		GetNode<Button>("%Gold").Pressed += () => _player.AddGold(1000);
		_go.Pressed += () => GoToFloor((int)_floor.Value);
		_next.Pressed += () => GoToFloor(_dungeon.CurrentFloor + 1);
		GetNode<Button>("%Close").Pressed += Close;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F1)
		{
			if (_backdrop.Visible)
				Close();
			else
			{
				_wasPaused = GetTree().Paused;
				_godMode.SetPressedNoSignal(_player.DebugGodMode);
				_floor.Value = _dungeon?.CurrentFloor ?? 1;
				_go.Disabled = _next.Disabled = _wasPaused || _dungeon == null || _player.health <= 0;
				_backdrop.Show();
				GetTree().Paused = true;
				_godMode.GrabFocus();
			}
			GetViewport().SetInputAsHandled();
		}
		else if (_backdrop.Visible && @event.IsActionPressed("ui_cancel"))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Close()
	{
		_backdrop.Hide();
		GetTree().Paused = _wasPaused;
	}

	private void GoToFloor(int floor)
	{
		Close();
		Callable.From(() => _dungeon.DebugGoToFloor(floor)).CallDeferred();
	}
}
