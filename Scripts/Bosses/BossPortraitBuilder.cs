using Godot;

public static class BossPortraitBuilder
{
	public static Texture2D FromSheet(BossConfig config)
	{
		var sheet = GD.Load<Texture2D>(config.SpriteSheetPath);
		if (sheet == null)
			return null;

		var image = sheet.GetImage();
		var source = image.GetRegion(new Rect2I(0, config.Animations[1].Row * config.FrameSize,
			config.FrameSize, config.FrameSize));
		return WithWhiteOutline(source);
	}

	public static Texture2D FromPortrait(string path, int frameSize)
	{
		var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
		if (image == null || image.IsEmpty())
			return GD.Load<Texture2D>(path);
		var cell = image.GetRegion(new Rect2I(0, 0, Mathf.Min(frameSize, image.GetWidth()), Mathf.Min(frameSize, image.GetHeight())));
		return WithWhiteOutline(cell);
	}

	private static ImageTexture WithWhiteOutline(Image source)
	{
		int width = source.GetWidth();
		int height = source.GetHeight();
		var outlined = Image.CreateEmpty(width + 2, height + 2, false, Image.Format.Rgba8);
		for (int y = 0; y < height; y++)
		for (int x = 0; x < width; x++)
		{
			if (source.GetPixel(x, y).A <= 0.0f)
				continue;
			for (int oy = -1; oy <= 1; oy++)
			for (int ox = -1; ox <= 1; ox++)
			{
				int px = x + ox + 1;
				int py = y + oy + 1;
				if (outlined.GetPixel(px, py).A <= 0.0f)
					outlined.SetPixel(px, py, Colors.White);
			}
		}
		for (int y = 0; y < height; y++)
		for (int x = 0; x < width; x++)
			if (source.GetPixel(x, y).A > 0.0f)
				outlined.SetPixel(x + 1, y + 1, source.GetPixel(x, y));
		return ImageTexture.CreateFromImage(outlined);
	}
}
