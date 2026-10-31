
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class UpgradeScene : Control
{
	private Player _player;

	// Add upgrade IDs here. Each ID must exist in _details
	// and be handled by Player.ApplyUpgrade().
	private readonly string[] _upgrades =
	{
		"damage_up",
		"speed_up",
		"max_health_up",
        "heal"
	};

	// Edit the card title and description here.
	private readonly Dictionary<string, (string title, string description)> _details =
		new()
		{
			{ "damage_up", ("Sharp Edge", "+5 Attack Damage") },
			{ "speed_up", ("Swift Feet", "+20 Movement Speed") },
			{ "max_health_up", ("Vitality", "+25 Max Health") },
			{ "heal", ("Healing", "Restore 30 Health") }
		};

	// Change this if you want a different number of upgrade choices.
	// Add the matching card nodes to Upgrade.tscn if you increase it.
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

		// Shuffle the upgrades and select unique choices.
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


		// Configure each upgrade card.
		for (int i = 0; i < CardCount; i++)
		{
			// Find the parent Control for this card.
			Control card = GetNodeOrNull<Control>(
				$"CenterContainer/VBoxContainer/Cards/Card{i + 1}"
			);

			if (card == null)
			{
				GD.PushError($"Could not find Card{i + 1}!");
				continue;
			}

			// Hide unused cards if there aren't enough choices.
			if (i >= choices.Length)
			{
				card.Hide();
				continue;
			}

			string upgradeId = choices[i];

			// Find the texture and text nodes inside the card.
			TextureButton button =
				card.GetNodeOrNull<TextureButton>("TextureButton");

			Label cardText =
				card.GetNodeOrNull<Label>("TextureButton/Label");

			if (button == null || cardText == null)
			{
				GD.PushError(
					$"Card{i + 1} needs a TextureButton and a Label child."
				);
				continue;
			}

			// Set the title and description displayed on the card.
			cardText.Text =
				$"{_details[upgradeId].title}\n\n" +
				$"{_details[upgradeId].description}";

			// Make sure the label doesn't block button clicks.
			cardText.MouseFilter = Control.MouseFilterEnum.Ignore;

			// Connect the click to the correct upgrade.
			button.Pressed += () => SelectUpgrade(upgradeId);
		}

	}

	private void SelectUpgrade(string upgradeId)
	{
		if (_player == null)
			return;

		// The actual stat changes happen in Player.ApplyUpgrade().
		_player.ApplyUpgrade(upgradeId);

		GD.Print($"Upgrade selected: {upgradeId}");

		// WaveManager listens for this Control leaving the tree.
		QueueFree();
	}
}
