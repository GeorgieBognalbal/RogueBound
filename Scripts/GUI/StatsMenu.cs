using Godot;
using System.Text;

public partial class StatsMenu : CanvasLayer
{
	private Label _statsLabel;
	private Button _closeButton;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_statsLabel = GetNode<Label>("Control/CenterContainer/VBoxContainer/ScrollContainer/StatsLabel");
		_closeButton = GetNode<Button>("Control/CenterContainer/VBoxContainer/CloseButton");

		_closeButton.Pressed += () => QueueFree();

		DisplayStats();
	}

	private void DisplayStats()
	{
		DatabaseManager db = GetNodeOrNull<DatabaseManager>("/root/DatabaseManager");
		if (db == null)
		{
			_statsLabel.Text = "DatabaseManager not found.";
			return;
		}

		var sb = new StringBuilder();

		// Lifetime stats
		var lifetime = db.GetLifetimeStatistics();
		sb.AppendLine("=== LIFETIME CAREER STATS ===");
		sb.AppendLine($"Total Waves Survived: {(lifetime.ContainsKey("total_waves") ? lifetime["total_waves"] : 0)}");
		sb.AppendLine($"Total Enemies Slain:  {(lifetime.ContainsKey("total_kills") ? lifetime["total_kills"] : 0)}");
		sb.AppendLine($"Highest Wave Reached: {(lifetime.ContainsKey("highest_wave") ? lifetime["highest_wave"] : 0)}");
		sb.AppendLine();

		// Last Run stats
		var lastRun = db.GetLastRunSummary();
		if (lastRun.Count > 0)
		{
			sb.AppendLine("=== MOST RECENT RUN ===");
			sb.AppendLine($"Result:         {lastRun["result"]}");
			sb.AppendLine($"Waves Cleared:  {lastRun["waves"]}");
			sb.AppendLine($"Enemies Slain:  {lastRun["total_kills"]}");

			var kills = db.GetLastRunEnemyKills();
			if (kills.Count > 0)
			{
				sb.AppendLine("\nKills by Enemy Type:");
				foreach (var item in kills)
				{
					sb.AppendLine($"  - {item["enemy_type"]}: {item["kills"]}");
				}
			}
		}
		else
		{
			sb.AppendLine("No recorded runs yet! Play a game to record stats.");
		}

		_statsLabel.Text = sb.ToString();
	}
}
