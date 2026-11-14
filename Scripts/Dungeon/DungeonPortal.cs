using Godot;

namespace RogueBound.Dungeon
{
	public partial class DungeonPortal : Area2D
	{
		[Export] public bool IsActive { get; set; } = false;

		private Sprite2D _sprite;

		public override void _Ready()
		{
			_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
			BodyEntered += OnBodyEntered;
			UpdateVisuals();
		}

		public void Activate()
		{
			IsActive = true;
			UpdateVisuals();
		}

		private void UpdateVisuals()
		{
			if (_sprite != null)
			{
				_sprite.Visible = IsActive;
			}
		}

		private void OnBodyEntered(Node2D body)
		{
			if (!IsActive || !(body is Player player))
				return;

			// Check if we have a DungeonManager in scene to advance floor
			var dm = GetTree().CurrentScene?.GetNodeOrNull<DungeonManager>("DungeonManager");
			if (dm != null)
			{
				dm.NextFloor();
				return;
			}

			// Trigger Victory via GameOverScene or DatabaseManager
			DatabaseManager db = GetNodeOrNull<DatabaseManager>("/root/DatabaseManager");
			if (db != null)
			{
				db.FinishRun("Victory");
			}

			var gameOverScene = GD.Load<PackedScene>("res://Scenes/Menus/GameOverMenu.tscn");
			if (gameOverScene != null && GetTree()?.CurrentScene != null)
			{
				GameOverMenu menu = gameOverScene.Instantiate<GameOverMenu>();
				GetTree().CurrentScene.AddChild(menu);
				menu.Setup(true, 1, 1, 0, player.Money);
			}
		}
	}
}
