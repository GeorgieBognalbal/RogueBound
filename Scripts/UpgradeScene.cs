
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class UpgradeScene : Control
{
	private Player _player;

	private readonly string[] _upgrades =
	{
		"damage_up",
		"speed_up",
		"max_health_up",
        "heal"
	};

	private readonly Dictionary<string, (string title, string description)> _details =
		new()
		{
			{ "damage_up", ("Sharp Edge", "+5 Attack Damage") },
			{ "speed_up", ("Swift Feet", "+20 Movement Speed") },
			{ "max_health_up", ("Vitality", "+25 Max Health") },
			{ "heal", ("Healing", "Restore 30 Health") }

		};

	private const int CardCount = 3;

	public override void _Ready()
	{
		// Find the player using the "player" group.
		_player = GetTree().GetFirstNodeInGroup("player") as Player;

		if (_player == null)
		{
			GD.PushError("UpgradeScene could not find the player!");
			return;
		}

		var choices = _upgrades
			.Distinct()
			.OrderBy(_ => GD.Randf())
			.Take(CardCount)
			.ToArray();

		if (choices.Length < CardCount)
		{
			GD.PushWarning(
				$"Only {choices.Length} unique upgrades available, " +
				$"but {CardCount} cards were requested."
			);
		}

		for (int i = 0; i < choices.Length; i++)
		{
			string upgradeId = choices[i];

			Button card = GetNode<Button>(
				$"CenterContainer/VBoxContainer/Cards/Card{i + 1}"
			);

			card.Text =
				$"{_details[upgradeId].title}\n\n" +
				$"{_details[upgradeId].description}";

			card.Pressed += () => SelectUpgrade(upgradeId);
		}

		for (int i = choices.Length; i < CardCount; i++)
		{
			Button card = GetNode<Button>(
				$"CenterContainer/VBoxContainer/Cards/Card{i + 1}"
			);

			card.Hide();
		}
	}
	private void SelectUpgrade(string upgradeId)
	{
		if (_player == null)
			return;

		_player.ApplyUpgrade(upgradeId);

		GD.Print($"Upgrade selected: {upgradeId}");

		// Closes this Control; your WaveManager listens for its
		// TreeExiting signal to unpause the game.
		QueueFree();
	}
}
