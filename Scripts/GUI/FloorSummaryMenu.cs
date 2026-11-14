using Godot;
using System;

public partial class FloorSummaryMenu : CanvasLayer
{
	public event Action ContinueRequested;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		BuildInterface();
	}

	public void Setup(int floor, int clearedRooms, int totalRooms, int kills, int coins)
	{
		var title = GetNode<Label>("Backdrop/Center/Panel/Margin/Content/Title");
		var stats = GetNode<Label>("Backdrop/Center/Panel/Margin/Content/Stats");
		var button = GetNode<Button>("Backdrop/Center/Panel/Margin/Content/ContinueButton");
		title.Text = $"FLOOR {floor} CLEARED";
		stats.Text = $"Rooms cleared: {clearedRooms} / {totalRooms}\nEnemies slain: {kills}\nCoins collected: {coins}";
		button.Text = $"Continue to Floor {floor + 1}";
	}

	private void BuildInterface()
	{
		var backdrop = new ColorRect
		{
			Name = "Backdrop",
			Color = new Color(0.015f, 0.02f, 0.04f, 0.86f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(backdrop);

		var center = new CenterContainer { Name = "Center", MouseFilter = Control.MouseFilterEnum.Ignore };
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		backdrop.AddChild(center);

		var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(480, 330) };
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.085f, 0.12f, 0.98f),
			BorderColor = new Color(0.34f, 0.73f, 0.83f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 12,
			CornerRadiusTopRight = 12,
			CornerRadiusBottomLeft = 12,
			CornerRadiusBottomRight = 12
		};
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		center.AddChild(panel);

		var margin = new MarginContainer { Name = "Margin" };
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_top", 28);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_bottom", 28);
		panel.AddChild(margin);

		var content = new VBoxContainer { Name = "Content", Alignment = BoxContainer.AlignmentMode.Center };
		content.AddThemeConstantOverride("separation", 22);
		margin.AddChild(content);

		var title = new Label
		{
			Name = "Title",
			Text = "FLOOR CLEARED",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 34);
		title.AddThemeColorOverride("font_color", new Color(0.46f, 0.86f, 0.92f));
		content.AddChild(title);

		var stats = new Label
		{
			Name = "Stats",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		stats.AddThemeFontSizeOverride("font_size", 22);
		content.AddChild(stats);

		var button = new Button { Name = "ContinueButton", Text = "Continue", CustomMinimumSize = new Vector2(230, 52) };
		button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		button.AddThemeFontSizeOverride("font_size", 20);
		button.Pressed += () =>
		{
			ContinueRequested?.Invoke();
			QueueFree();
		};
		content.AddChild(button);
		button.GrabFocus();
	}
}
