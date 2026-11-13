using Godot;
using System.Collections.Generic;

namespace RogueBound.Dungeon
{
	public partial class PoisonGas : Area2D
	{
		[Export] public float DamagePerTick = 5.0f;
		[Export] public float TickInterval = 1.0f;
		[Export] public bool IsActive { get; private set; } = false;

		private ColorRect _fogRect;
		private CollisionShape2D _collision;
		private float _tickTimer = 0.0f;
		private float _pulseTime = 0.0f;
		private readonly List<Player> _overlappingPlayers = new();

		public override void _Ready()
		{
			_fogRect = GetNodeOrNull<ColorRect>("FogRect");
			_collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

			BodyEntered += OnBodyEntered;
			BodyExited += OnBodyExited;

			if (_fogRect != null)
				_fogRect.Modulate = new Color(0.2f, 0.9f, 0.2f, 0.0f);
		}

		public void SetupBounds(Vector2 size)
		{
			if (_collision != null)
			{
				var shape = new RectangleShape2D { Size = size };
				_collision.Shape = shape;
			}

			if (_fogRect != null)
			{
				_fogRect.Size = size;
				_fogRect.Position = -size * 0.5f;
			}
		}

		public void Activate(float initialAlpha = 0.35f)
		{
			IsActive = true;
			_tickTimer = TickInterval;
			if (_fogRect != null)
			{
				_fogRect.Modulate = new Color(0.2f, 0.9f, 0.2f, initialAlpha);
			}
		}

		public void Disperse()
		{
			IsActive = false;
			_overlappingPlayers.Clear();

			if (_fogRect != null)
			{
				var tween = CreateTween();
				tween.TweenProperty(_fogRect, "modulate:a", 0.0f, 0.8f);
			}
		}

		private void OnBodyEntered(Node2D body)
		{
			if (body is Player player && !_overlappingPlayers.Contains(player))
			{
				_overlappingPlayers.Add(player);
			}
		}

		private void OnBodyExited(Node2D body)
		{
			if (body is Player player)
			{
				_overlappingPlayers.Remove(player);
			}
		}

		public override void _PhysicsProcess(double delta)
		{
			if (!IsActive)
				return;

			float dt = (float)delta;
			_pulseTime += dt * 3.0f;

			// Visual pulsing
			if (_fogRect != null)
			{
				float alpha = 0.35f + Mathf.Sin(_pulseTime) * 0.1f;
				_fogRect.Modulate = new Color(0.2f, 0.9f, 0.2f, alpha);
			}

			// Damage ticking
			_tickTimer -= dt;
			if (_tickTimer <= 0.0f)
			{
				_tickTimer = TickInterval;

				foreach (var player in _overlappingPlayers)
				{
					if (GodotObject.IsInstanceValid(player))
					{
						player.TakeDamage(DamagePerTick);
					}
				}
			}
		}
	}
}
