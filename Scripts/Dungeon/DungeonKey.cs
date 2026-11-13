using Godot;
using System;

namespace RogueBound.Dungeon
{
	public partial class DungeonKey : Area2D
	{
		public event Action Collected;

		private Sprite2D _sprite;
		private AudioStreamPlayer2D _audio;
		private bool _isCollected = false;

		public override void _Ready()
		{
			_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
			_audio = GetNodeOrNull<AudioStreamPlayer2D>("PickupAudio");
			BodyEntered += OnBodyEntered;
		}

		private void OnBodyEntered(Node2D body)
		{
			if (_isCollected || !(body is Player player))
				return;

			_isCollected = true;

			// Visual / floating text
			var dmgScene = GD.Load<PackedScene>("res://Scenes/Effects/DamageNumber.tscn");
			if (dmgScene != null && GetTree().CurrentScene != null)
			{
				Node2D text = dmgScene.Instantiate<Node2D>();
				GetTree().CurrentScene.AddChild(text);
				text.GlobalPosition = GlobalPosition + new Vector2(0, -15);
				text.Call("ShowDamage", 999); // Will show key pickup feedback
			}

			if (_audio != null && _audio.Stream != null)
				_audio.Play();

			Collected?.Invoke();

			// Slight delay before queue free to allow sound
			SetDeferred(Area2D.PropertyName.Monitoring, false);
			Visible = false;
			GetTree().CreateTimer(0.5f).Timeout += () => QueueFree();
		}
	}
}
