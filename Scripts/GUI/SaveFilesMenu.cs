using Godot;
using System;

public partial class SaveFilesMenu : CanvasLayer
{
	private DatabaseManager _database;
	private GridContainer _saveTable;
	private Label _emptyState;
	private LineEdit _nameInput;
	private Label _message;
	private Label _selectedSaveName;
	private Label _totalWaves;
	private Label _totalKills;
	private Label _bestWave;
	private GridContainer _latestRunGrid;
	private Label _latestRunEmpty;
	private Button _addButton;
	private Button _createButton;
	private Button _cancelCreateButton;
	private ConfirmationDialog _deleteDialog;
	private long _pendingDeleteId = -1;
	private bool _playAfterCreate;
	public bool PlayIntent { get; set; }

	public override void _Ready()
	{
		_database = GetNode<DatabaseManager>("/root/DatabaseManager");
		_saveTable = GetNode<GridContainer>("CenterContainer/Panel/Margins/Content/SaveScroll/SaveContents/TableCenter/SaveTable");
		_emptyState = GetNode<Label>("CenterContainer/Panel/Margins/Content/SaveScroll/SaveContents/EmptyState");
		_nameInput = GetNode<LineEdit>("CenterContainer/Panel/Margins/Content/CreateRow/NameInput");
		_message = GetNode<Label>("CenterContainer/Panel/Margins/Content/Message");
		_selectedSaveName = GetNode<Label>("CenterContainer/Panel/Margins/Content/StatsPanel/StatsMargins/StatsContent/SelectedSaveName");
		_totalWaves = GetNode<Label>("CenterContainer/Panel/Margins/Content/StatsPanel/StatsMargins/StatsContent/CareerStats/TotalWaves/Value");
		_totalKills = GetNode<Label>("CenterContainer/Panel/Margins/Content/StatsPanel/StatsMargins/StatsContent/CareerStats/TotalKills/Value");
		_bestWave = GetNode<Label>("CenterContainer/Panel/Margins/Content/StatsPanel/StatsMargins/StatsContent/CareerStats/BestWave/Value");
		_latestRunGrid = GetNode<GridContainer>("CenterContainer/Panel/Margins/Content/StatsPanel/StatsMargins/StatsContent/LatestRunCenter/LatestRunGrid");
		_latestRunEmpty = GetNode<Label>("CenterContainer/Panel/Margins/Content/StatsPanel/StatsMargins/StatsContent/LatestRunEmpty");
		_addButton = GetNode<Button>("CenterContainer/Panel/Margins/Content/CreateRow/AddButton");
		_createButton = GetNode<Button>("CenterContainer/Panel/Margins/Content/CreateRow/CreateButton");
		_cancelCreateButton = GetNode<Button>("CenterContainer/Panel/Margins/Content/CreateRow/CancelButton");
		_deleteDialog = GetNode<ConfirmationDialog>("DeleteDialog");

		_addButton.Pressed += ShowCreateForm;
		_createButton.Pressed += CreateSave;
		_cancelCreateButton.Pressed += HideCreateForm;
		_nameInput.TextSubmitted += _ => CreateSave();
		_nameInput.VisibilityChanged += SyncCreateButtons;
		GetNode<Button>("CenterContainer/Panel/Margins/Content/Footer/BackButton").Pressed += QueueFree;
		_deleteDialog.Confirmed += ConfirmDelete;
		ApplyButtonStyle(_addButton);
		ApplyButtonStyle(_createButton);
		ApplyButtonStyle(_cancelCreateButton);
		ApplyButtonStyle(GetNode<Button>("CenterContainer/Panel/Margins/Content/Footer/BackButton"));
		var panel = GetNode<PanelContainer>("CenterContainer/Panel");
		panel.Theme = BuildTheme();
		ClearStats();
		RefreshSaveList();
		if (_database.GetSaveFiles().Count == 0)
		{
			_message.Text = "Create your first save file to begin playing.";
			_playAfterCreate = PlayIntent;
			ShowCreateForm();
		}
	}

	private Theme BuildTheme()
	{
		var theme = new Theme();
		theme.SetColor("font_color", "Label", new Color(0.91f, 0.94f, 1f));
		theme.SetColor("font_color", "Button", new Color(0.96f, 0.96f, 1f));
		theme.SetColor("font_hover_color", "Button", new Color(1f, 0.83f, 0.46f));
		return theme;
	}

	private void ApplyButtonStyle(Button button)
	{
		button.FocusMode = Control.FocusModeEnum.None;
		button.AddThemeFontSizeOverride("font_size", 12);
		button.MouseEntered += () => AnimateScale(button, new Vector2(1.055f, 1.055f));
		button.MouseExited += () => AnimateScale(button, Vector2.One);
	}

	private void AnimateScale(Control control, Vector2 target)
	{
		control.PivotOffset = control.Size / 2f;
		var tween = CreateTween();
		tween.TweenProperty(control, "scale", target, 0.14).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	private void SyncCreateButtons()
	{
		_createButton.Visible = _nameInput.Visible;
		_cancelCreateButton.Visible = _nameInput.Visible;
		_addButton.Visible = !_nameInput.Visible;
	}

	private void ShowCreateForm()
	{
		_nameInput.Visible = true;
		SyncCreateButtons();
		_nameInput.GrabFocus();
	}

	private void HideCreateForm()
	{
		_nameInput.Visible = false;
		_nameInput.Clear();
		_message.Text = "";
		SyncCreateButtons();
	}

	private void CreateSave()
	{
		string name = _nameInput.Text.Trim();
		if (name.Length == 0) { _message.Text = "Enter a name for this save file."; return; }
		long id = _database.CreateSaveFile(name);
		if (id < 0) { _message.Text = "That name is already in use, or the name is invalid. Use up to 32 characters."; return; }
		_nameInput.Clear();
		_nameInput.Visible = false;
		SyncCreateButtons();
		_message.Text = $"Save file '{name}' created.";
		RefreshSaveList();
		ShowSaveStats(id);
		if (_playAfterCreate) { _playAfterCreate = false; LaunchSave(id); }
	}

	private void RefreshSaveList()
	{
		while (_saveTable.GetChildCount() > 6)
		{
			Node child = _saveTable.GetChild(_saveTable.GetChildCount() - 1);
			_saveTable.RemoveChild(child);
			child.QueueFree();
		}
		var saves = _database.GetSaveFiles();
		_saveTable.Visible = saves.Count > 0;
		_emptyState.Visible = saves.Count == 0;
		foreach (Godot.Collections.Dictionary save in saves)
		{
			long id = (long)save["id"];
			string name = (string)save["name"];
			_saveTable.AddChild(MakeColumnLabel(name, 260));
			_saveTable.AddChild(MakeColumnLabel($"{save["total_kills"]}", 120, true));
			_saveTable.AddChild(MakeColumnLabel($"{save["highest_wave"]}", 120, true));
			var play = MakeRowButton("Play"); play.Pressed += () => LaunchSave(id); _saveTable.AddChild(play);
			var stats = MakeRowButton("Stats"); stats.Pressed += () => ShowSaveStats(id); _saveTable.AddChild(stats);
			var delete = MakeRowButton("Delete"); delete.Pressed += () => AskDelete(id, name); _saveTable.AddChild(delete);
		}
	}

	private Button MakeRowButton(string text)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(80, 34), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		ApplyButtonStyle(button);
		return button;
	}

	private Label MakeColumnLabel(string text, float width, bool centered = false)
	{
		var label = new Label
		{
			Text = text,
			CustomMinimumSize = new Vector2(width, 34),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		return label;
	}

	private void ShowSaveStats(long id)
	{
		var info = _database.GetSaveFileSummary(id);
		if (info.Count == 0) { ClearStats(); _selectedSaveName.Text = "Save file not found"; return; }
		_selectedSaveName.Text = (string)info["name"];
		_totalWaves.Text = $"{info["total_waves"]}";
		_totalKills.Text = $"{info["total_kills"]}";
		_bestWave.Text = $"{info["highest_wave"]}";

		bool hasLatestRun = info.ContainsKey("last_result");
		_latestRunGrid.Visible = hasLatestRun;
		_latestRunEmpty.Visible = !hasLatestRun;
		if (hasLatestRun)
		{
			_latestRunGrid.GetNode<Label>("ResultValue").Text = (string)info["last_result"];
			_latestRunGrid.GetNode<Label>("WavesValue").Text = $"{info["last_waves"]}";
			_latestRunGrid.GetNode<Label>("KillsValue").Text = $"{info["last_kills"]}";
			_latestRunGrid.GetNode<Label>("DateValue").Text = FormatDate((string)info["last_date"]);
		}
	}

	private string FormatDate(string isoDate)
		=> DateTime.TryParse(isoDate, out var date) ? date.ToLocalTime().ToString("MMM d, yyyy") : isoDate;

	private void AskDelete(long id, string name)
	{
		_pendingDeleteId = id;
		_deleteDialog.DialogText = $"Delete '{name}' and all of its run history? This cannot be undone.";
		_deleteDialog.PopupCentered();
	}

	private void ConfirmDelete()
	{
		if (_database.DeleteSaveFile(_pendingDeleteId))
		{
			_message.Text = "Save file deleted.";
			ClearStats();
			RefreshSaveList();
		}
		_pendingDeleteId = -1;
	}

	private void ClearStats()
	{
		_selectedSaveName.Text = "Select a save file";
		_totalWaves.Text = "—";
		_totalKills.Text = "—";
		_bestWave.Text = "—";
		_latestRunGrid.Visible = false;
		_latestRunEmpty.Visible = true;
	}

	private async void LaunchSave(long id)
	{
		_database.SelectSaveFile(id);
		var transition = GetNodeOrNull<SceneTransition>("/root/SceneTransition");
		if (transition != null) await transition.ChangeSceneWithFade("res://Scenes/game.tscn");
		else GetTree().ChangeSceneToFile("res://Scenes/game.tscn");
	}
}
