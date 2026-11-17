using Godot;

// Run from the Godot editor with File > Run (or the script's Run button).
[Tool]
public partial class GenerateIntroFrames : EditorScript
{
	private const int FrameWidth = 320;
	private const int FrameHeight = 180;
	private const int TileSize = 16;
	private const int CharacterScale = 3;

	// Shared 24-color palette for all hand-drawn pixels.
	private static readonly Color Ink = Hex("17182b");
	private static readonly Color DeepBlue = Hex("20294a");
	private static readonly Color NightBlue = Hex("29375d");
	private static readonly Color SkyBlue = Hex("6badd0");
	private static readonly Color Cloud = Hex("b9d6ce");
	private static readonly Color Moon = Hex("e8dda8");
	private static readonly Color Star = Hex("f3e8bd");
	private static readonly Color Grass = Hex("497346");
	private static readonly Color GrassLight = Hex("83a052");
	private static readonly Color LeafDark = Hex("28483d");
	private static readonly Color Stone = Hex("55556d");
	private static readonly Color StoneLight = Hex("858194");
	private static readonly Color StoneHighlight = Hex("aaa3a0");
	private static readonly Color StoneShadow = Hex("36374e");
	private static readonly Color WindowGold = Hex("f4cc72");
	private static readonly Color Red = Hex("9f3548");
	private static readonly Color FireRed = Hex("cf493c");
	private static readonly Color FireOrange = Hex("f07b38");
	private static readonly Color FireYellow = Hex("f6c75c");
	private static readonly Color Smoke = Hex("555065");
	private static readonly Color PartnerGreen = Hex("4a9b71");
	private static readonly Color PartnerLight = Hex("d5e6c8");
	private static readonly Color Skin = Hex("e4b08b");
	private static readonly Color Purple = Hex("765b83");

	private static Image _stoneAtlas;
	private static Image _grassAtlas;
	private static Image _knightIdle;
	private static Image _knightWalk;
	private static Image _knightSmall;
	private static Image _orc;
	private static Image _enemySprite;
	private static Image _partnerSprite;

	public override void _Run()
	{
		if (GenerateImages(true).Length != 8)
			return;
		GD.Print("Generated 8 intro frames in res://Assets/Intro/");
	}

	// Also used by the intro player so frames can be shown in memory in exported builds.
	public static Image[] GenerateImages(bool savePngs)
	{
		if (!LoadSourceAssets())
			return System.Array.Empty<Image>();

		if (savePngs)
		{
			string directory = ProjectSettings.GlobalizePath("res://Assets/Intro");
			Error makeDirectory = DirAccess.MakeDirRecursiveAbsolute(directory);
			if (makeDirectory != Error.Ok && makeDirectory != Error.AlreadyExists)
			{
				GD.PushError($"Could not create intro frame directory: {makeDirectory}");
				return System.Array.Empty<Image>();
			}
		}

		var frames = new Image[8];
		for (int index = 0; index < frames.Length; index++)
		{
			Image frame = Image.CreateEmpty(FrameWidth, FrameHeight, false, Image.Format.Rgba8);
			DrawFrame(frame, index + 1);
			frames[index] = frame;
			if (savePngs)
			{
				Error result = frame.SavePng($"res://Assets/Intro/frame{index + 1}.png");
				if (result != Error.Ok)
					GD.PushError($"Could not save intro frame {index + 1}: {result}");
			}
		}
		return frames;
	}

	private static bool LoadSourceAssets()
	{
		_stoneAtlas = LoadImage("res://Assets/Temp/TX Tileset Stone Ground.png");
		_grassAtlas = LoadImage("res://Assets/Temp/TX Tileset Grass.png");
		Image soldierIdleSheet = LoadImage("res://Assets/Temp/Soldier/Soldier.png");
		Image soldierWalkSheet = LoadImage("res://Assets/Temp/Soldier/Soldier_Walk.png");
		Image orcSheet = LoadImage("res://Assets/Temp/Orc/Orc with shadows/Orc_Idle.png");
		Image partnerSheet = LoadOptionalImage("res://Assets/Intro/partner_girl_32x32.png");

		// These are the actual 100x100 atlas cells referenced by the Player scene.
		_knightIdle = ExtractSprite(soldierIdleSheet, new Rect2I(100, 0, 100, 100), CharacterScale);
		_knightWalk = ExtractSprite(soldierWalkSheet, new Rect2I(0, 0, 100, 100), CharacterScale);
		_orc = ExtractSprite(orcSheet, new Rect2I(0, 0, 100, 100), CharacterScale);
		_partnerSprite = partnerSheet != null
			? ExtractSprite(partnerSheet, new Rect2I(0, 0, 32, 32), 1)
			: null;
		if (_knightIdle != null)
		{
			_knightSmall = ResizeToHeight(_knightIdle, 34);
			if (_partnerSprite != null)
				_partnerSprite = ResizeToHeight(_partnerSprite, _knightIdle.GetHeight());
		}
		_enemySprite = _orc != null ? MakeOpaqueEnemySprite(_orc) : null;

		if (_stoneAtlas == null || _grassAtlas == null || _knightIdle == null || _knightWalk == null || _enemySprite == null)
		{
			GD.PushError("Intro generation needs the project's stone, grass, Soldier, and Orc PNG assets.");
			return false;
		}
		if (_partnerSprite == null)
			GD.PushWarning("Partner sprite is missing at res://Assets/Intro/partner_girl_32x32.png; generated frames will omit the partner until it is added.");
		return true;
	}

	private static Image LoadImage(string path)
	{
		Texture2D texture = GD.Load<Texture2D>(path);
		Image image = texture?.GetImage();
		if (image == null || image.IsEmpty())
		{
			GD.PushError($"Could not load intro source image: {path}");
			return null;
		}
		return image;
	}

	private static Image LoadOptionalImage(string path)
	{
		if (!FileAccess.FileExists(path))
			return null;
		Texture2D texture = GD.Load<Texture2D>(path);
		Image image = texture?.GetImage();
		return image == null || image.IsEmpty() ? null : image;
	}

	private static Image ExtractSprite(Image sheet, Rect2I cell, int scale)
	{
		if (sheet == null)
			return null;

		Image cellImage = Image.CreateEmpty(cell.Size.X, cell.Size.Y, true, Image.Format.Rgba8);
		cellImage.BlitRect(sheet, cell, Vector2I.Zero);
		Rect2I used = cellImage.GetUsedRect();
		if (used.Size == Vector2I.Zero)
			return null;

		Image trimmed = Image.CreateEmpty(used.Size.X, used.Size.Y, true, Image.Format.Rgba8);
		trimmed.BlitRect(cellImage, used, Vector2I.Zero);
		trimmed.Resize(trimmed.GetWidth() * scale, trimmed.GetHeight() * scale, Image.Interpolation.Nearest);
		return trimmed;
	}

	private static void DrawFrame(Image image, int frame)
	{
		switch (frame)
		{
			case 1:
				DrawCastleDay(image);
				break;
			case 2:
				DrawCastleDayBackground(image);
				DrawSmallCastle(image);
				DrawSprite(image, _knightIdle, new Vector2I(137, 110));
				DrawPartnerSprite(image, 212, 164, false);
				DrawPixelHeart(image, 179, 94);
				break;
			case 3:
				DrawCastleNight(image);
				break;
			case 4:
				DrawBurningCastle(image);
				break;
			case 5:
				DrawBurningCourtyard(image);
				DrawSprite(image, _knightWalk, new Vector2I(88, 99), true);
				DrawPixelSword(image, 125, 111);
				break;
			case 6:
				DrawCaptureScene(image);
				break;
			case 7:
				DrawDungeonInfiltration(image);
				break;
			case 8:
				DrawDungeon(image);
				DrawSprite(image, _knightIdle, new Vector2I(134, 103));
				DrawPixelSword(image, 176, 112);
				break;
		}
	}

	private static void DrawCastleDayBackground(Image image)
	{
		image.Fill(SkyBlue);
		FillCircle(image, 260, 39, 20, WindowGold);
		DrawCloud(image, 38, 43);
		DrawCloud(image, 229, 77);
		DrawHill(image, false);
	}

	private static void DrawSmallCastle(Image image)
	{
		FillRect(image, 22, 90, 101, 69, Ink);
		FillRect(image, 29, 97, 87, 58, Stone);
		FillRect(image, 36, 111, 70, 45, StoneLight);
		FillRect(image, 25, 96, 23, 62, StoneLight);
		FillRect(image, 97, 87, 23, 71, StoneLight);
		TileRect(image, _stoneAtlas, 36, 111, 70, 45, 0, 0);
		TileRect(image, _stoneAtlas, 25, 96, 23, 62, 16, 16);
		TileRect(image, _stoneAtlas, 97, 87, 23, 71, 32, 16);
		for (int x = 25; x < 49; x += 12) FillRect(image, x, 89, 8, 8, StoneLight);
		for (int x = 97; x < 121; x += 12) FillRect(image, x, 80, 8, 8, StoneLight);
		for (int x = 22; x < 123; x += 15) FillRect(image, x, 103, 9, 8, Stone);
		FillRect(image, 64, 134, 19, 24, Ink);
		FillRect(image, 68, 138, 11, 20, StoneShadow);
		FillRect(image, 50, 119, 9, 15, Ink);
		FillRect(image, 52, 121, 5, 10, StoneShadow);
		FillRect(image, 88, 119, 9, 15, Ink);
		FillRect(image, 90, 121, 5, 10, StoneShadow);
		DrawSmallBanner(image, 34, 73, Red);
		DrawSmallBanner(image, 106, 64, PartnerGreen);
	}

	private static void DrawSmallBanner(Image image, int x, int y, Color color)
	{
		FillRect(image, x, y, 2, 22, Ink);
		FillRect(image, x + 2, y + 2, 12, 15, Ink);
		FillRect(image, x + 4, y + 4, 8, 10, color);
		FillRect(image, x + 4, y + 4, 2, 7, PartnerLight);
	}

	private static Image ResizeToHeight(Image source, int targetHeight)
	{
		if (source == null || source.IsEmpty() || targetHeight <= 0)
			return source;
		int targetWidth = Mathf.Max(1, Mathf.RoundToInt((float)source.GetWidth() * targetHeight / source.GetHeight()));
		Image resized = Image.CreateEmpty(source.GetWidth(), source.GetHeight(), true, Image.Format.Rgba8);
		resized.BlitRect(source, new Rect2I(0, 0, source.GetWidth(), source.GetHeight()), Vector2I.Zero);
		resized.Resize(targetWidth, targetHeight, Image.Interpolation.Nearest);
		return resized;
	}

	private static Image MakeOpaqueEnemySprite(Image source)
	{
		int width = source.GetWidth();
		int height = source.GetHeight();
		Image outlined = Image.CreateEmpty(width + 2, height + 2, true, Image.Format.Rgba8);
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (source.GetPixel(x, y).A <= 0.05f)
					continue;
				for (int oy = -1; oy <= 1; oy++)
					for (int ox = -1; ox <= 1; ox++)
						outlined.SetPixel(x + ox + 1, y + oy + 1, Ink);
			}
		}
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (source.GetPixel(x, y).A > 0.05f)
					outlined.SetPixel(x + 1, y + 1, StoneShadow);
			}
		}
		return outlined;
	}

	private static void DrawCastleDay(Image image)
	{
		image.Fill(SkyBlue);
		FillCircle(image, 260, 39, 20, WindowGold);
		DrawCloud(image, 38, 43);
		DrawCloud(image, 229, 77);
		DrawHill(image, false);
		DrawCastle(image, false, false);
	}

	private static void DrawCastleNight(Image image)
	{
		image.Fill(DeepBlue);
		DrawStars(image);
		FillCircle(image, 268, 35, 17, Moon);
		FillCircle(image, 275, 28, 16, DeepBlue);
		FillRect(image, 0, 133, FrameWidth, 16, Red);
		DrawHill(image, true);
		DrawCastle(image, true, false);
	}

	private static void DrawCastle(Image image, bool night, bool burning)
	{
		Color wall = night ? StoneShadow : Stone;
		Color lit = night ? Stone : StoneLight;
		// One-pixel dark outline around the keep and its two towers.
		FillRect(image, 82, 77, 156, 82, Ink);
		FillRect(image, 91, 63, 34, 96, Ink);
		FillRect(image, 195, 58, 34, 101, Ink);
		FillRect(image, 98, 72, 20, 82, lit);
		FillRect(image, 202, 67, 20, 87, lit);
		FillRect(image, 89, 87, 142, 70, wall);
		FillRect(image, 96, 94, 128, 56, lit);
		TileRect(image, _stoneAtlas, 96, 94, 128, 56, 0, 0);
		TileRect(image, _stoneAtlas, 98, 72, 20, 82, 16, 16);
		TileRect(image, _stoneAtlas, 202, 67, 20, 87, 32, 16);

		for (int x = 93; x < 126; x += 16) FillRect(image, x, 57, 10, 10, lit);
		for (int x = 197; x < 230; x += 16) FillRect(image, x, 52, 10, 10, lit);
		for (int x = 82; x < 230; x += 16) FillRect(image, x, 77, 10, 10, wall);

		FillRect(image, 143, 111, 34, 46, Ink);
		FillRect(image, 148, 116, 24, 41, night ? DeepBlue : StoneShadow);
		for (int x = 116; x <= 190; x += 37)
		{
			FillRect(image, x, 101, 12, 18, Ink);
			FillRect(image, x + 3, 104, 6, 12, night ? WindowGold : StoneShadow);
		}
		if (night)
		{
			FillRect(image, 104, 102, 7, 10, WindowGold);
			FillRect(image, 208, 97, 7, 10, WindowGold);
		}

		// Banners use a dark outline, bright cloth, and a single highlight stripe.
		DrawBanner(image, 109, 40, Red);
		DrawBanner(image, 213, 35, PartnerGreen);
		if (burning)
		{
			DrawSmoke(image, 98, 24);
			DrawSmoke(image, 211, 15);
			DrawFlame(image, 107, 48, 26);
			DrawFlame(image, 211, 43, 30);
			DrawFlame(image, 158, 72, 19);
		}
	}

	private static void DrawHill(Image image, bool dark)
	{
		Color farGrass = dark ? LeafDark : Grass;
		Color nearGrass = dark ? DeepBlue : GrassLight;
		FillCircle(image, 39, 160, 54, farGrass);
		FillCircle(image, 270, 157, 68, farGrass);
		FillRect(image, 0, 150, FrameWidth, 30, farGrass);
		FillRect(image, 0, 167, FrameWidth, 13, nearGrass);
		TileRect(image, _grassAtlas, 0, 167, FrameWidth, 13, 0, 0);
		for (int i = 0; i < 28; i++)
			SetPixel(image, (i * 37) % FrameWidth, 166 + (i % 2), dark ? LeafDark : GrassLight);
	}

	private static void DrawCloud(Image image, int x, int y)
	{
		FillRect(image, x, y + 6, 49, 12, Cloud);
		FillCircle(image, x + 12, y + 8, 10, Cloud);
		FillCircle(image, x + 29, y + 2, 13, Cloud);
		FillCircle(image, x + 42, y + 9, 8, Cloud);
		FillRect(image, x + 4, y + 15, 38, 3, SkyBlue);
	}

	private static void DrawBanner(Image image, int x, int y, Color color)
	{
		FillRect(image, x, y, 3, 31, Ink);
		FillRect(image, x + 3, y + 3, 16, 23, Ink);
		FillRect(image, x + 5, y + 5, 12, 16, color);
		FillRect(image, x + 5, y + 5, 3, 13, PartnerLight);
		FillRect(image, x + 10, y + 21, 3, 5, Ink);
	}

	private static void DrawBurningCastle(Image image)
	{
		image.Fill(DeepBlue);
		FillRect(image, 0, 0, FrameWidth, 111, Red);
		FillRect(image, 0, 92, FrameWidth, 35, FireRed);
		DrawSmoke(image, 39, 5);
		DrawSmoke(image, 259, 10);
		DrawStars(image);
		DrawHill(image, true);
		DrawCastle(image, true, true);
		DrawEmbers(image);
	}

	private static void DrawBurningCourtyard(Image image)
	{
		image.Fill(DeepBlue);
		FillRect(image, 0, 0, FrameWidth, 134, Red);
		FillRect(image, 0, 120, FrameWidth, 60, StoneShadow);
		FillRect(image, 0, 145, FrameWidth, 35, Ink);
		// Broken masonry, scorched paving, and burning debris.
		TileRect(image, _stoneAtlas, 0, 150, FrameWidth, 30, 32, 16);
		FillRect(image, 0, 69, 49, 82, Ink);
		FillRect(image, 5, 75, 34, 73, StoneShadow);
		TileRect(image, _stoneAtlas, 5, 75, 34, 73, 16, 16);
		FillRect(image, 0, 69, 21, 8, StoneLight);
		FillRect(image, 19, 62, 19, 8, Stone);
		FillRect(image, 274, 63, 46, 88, Ink);
		FillRect(image, 281, 70, 39, 78, StoneShadow);
		TileRect(image, _stoneAtlas, 281, 70, 39, 78, 32, 16);
		FillRect(image, 281, 64, 22, 8, StoneLight);
		FillRect(image, 303, 57, 17, 8, Stone);
		DrawFlame(image, 29, 109, 28);
		DrawFlame(image, 255, 96, 36);
		DrawFlame(image, 215, 130, 19);
		FillRect(image, 41, 141, 27, 7, Ink);
		FillRect(image, 244, 145, 31, 8, Ink);
		FillRect(image, 158, 151, 17, 4, StoneLight);
		DrawSmoke(image, 37, 11);
		DrawSmoke(image, 272, 4);
		DrawEmbers(image);
	}

	private static void DrawCaptureScene(Image image)
	{
		DrawBurningCourtyard(image);
		DrawSprite(image, _knightSmall, new Vector2I(23, 119));
		DrawLine(image, 49, 135, 70, 128, StoneLight);
		FillRect(image, 68, 126, 5, 4, StoneHighlight);
		DrawEnemy(image, 111, 96);
		DrawPartnerSprite(image, 166, 151, true);
		DrawEnemy(image, 224, 100);
	}

	private static void DrawDungeonInfiltration(Image image)
	{
		DrawDungeon(image);
		DrawSprite(image, _knightSmall, new Vector2I(67, 101));
		DrawEnemy(image, 105, 105);
		DrawPartnerSprite(image, 165, 154, true);
		DrawEnemy(image, 197, 101);

		// A foreground rock outcrop hides the knight's body while he peeks around the entrance.
		FillRect(image, 63, 120, 42, 60, Ink);
		FillRect(image, 68, 124, 33, 56, Stone);
		TileRect(image, _stoneAtlas, 68, 124, 33, 56, 16, 16);
		FillRect(image, 63, 117, 11, 7, StoneLight);
		FillRect(image, 74, 121, 12, 4, StoneLight);
		FillRect(image, 86, 123, 19, 4, Stone);
	}

	private static void DrawDungeon(Image image)
	{
		image.Fill(DeepBlue);
		FillRect(image, 0, 0, FrameWidth, FrameHeight, NightBlue);
		// Reuse the game's 16x16 stone atlas as a rocky wall, with its floor stones below.
		TileRect(image, _stoneAtlas, 0, 0, FrameWidth, 148, 16, 16);
		TileRect(image, _stoneAtlas, 0, 148, FrameWidth, 32, 32, 32);
		FillRect(image, 0, 0, FrameWidth, 31, DeepBlue);
		DrawStars(image);
		// Carved entrance: a dark inset framed by the same stone tiles.
		FillRect(image, 91, 73, 138, 107, Ink);
		FillRect(image, 99, 82, 122, 98, DeepBlue);
		TileRect(image, _stoneAtlas, 92, 74, 16, 104, 16, 16);
		TileRect(image, _stoneAtlas, 212, 74, 16, 104, 32, 16);
		TileRect(image, _stoneAtlas, 105, 72, 110, 16, 48, 16);
		FillRect(image, 119, 105, 82, 75, Ink);
		FillRect(image, 128, 114, 64, 66, DeepBlue);
		FillRect(image, 149, 145, 22, 35, Ink);
		DrawLine(image, 22, 53, 31, 58, StoneLight);
		DrawLine(image, 31, 58, 37, 56, StoneLight);
		DrawLine(image, 53, 92, 61, 101, StoneShadow);
		DrawLine(image, 61, 101, 73, 99, StoneShadow);
		DrawLine(image, 245, 54, 258, 64, StoneLight);
		DrawLine(image, 258, 64, 267, 60, StoneLight);
		DrawLine(image, 251, 113, 260, 121, StoneShadow);
		DrawLine(image, 260, 121, 271, 118, StoneShadow);
		DrawTorch(image, 80, 107);
		DrawTorch(image, 231, 107);
	}

	private static void DrawPartnerSprite(Image image, int centerX, int bottomY, bool carried)
	{
		if (_partnerSprite == null)
			return;
		Image pose = carried ? RotateClockwise(_partnerSprite) : _partnerSprite;
		int x = centerX - pose.GetWidth() / 2;
		int y = bottomY - pose.GetHeight();
		DrawSprite(image, pose, new Vector2I(x, y));
	}

	private static Image RotateClockwise(Image source)
	{
		Image rotated = Image.CreateEmpty(source.GetHeight(), source.GetWidth(), true, Image.Format.Rgba8);
		for (int y = 0; y < source.GetHeight(); y++)
		{
			for (int x = 0; x < source.GetWidth(); x++)
			{
				rotated.SetPixel(source.GetHeight() - 1 - y, x, source.GetPixel(x, y));
			}
		}
		return rotated;
	}

	private static void DrawEnemy(Image image, int x, int y)
	{
		DrawSprite(image, _enemySprite, new Vector2I(x, y));
		// Fully opaque square eye pixels, laid over the outlined Orc sprite.
		FillRect(image, x + 24, y + 21, 4, 3, FireOrange);
		FillRect(image, x + 40, y + 21, 4, 3, FireOrange);
		SetPixel(image, x + 25, y + 21, FireYellow);
		SetPixel(image, x + 41, y + 21, FireYellow);
	}

	private static void DrawTorch(Image image, int x, int y)
	{
		FillRect(image, x, y + 13, 5, 26, Ink);
		FillRect(image, x - 4, y + 35, 13, 4, StoneLight);
		DrawFlame(image, x + 2, y - 1, 17);
		FillRect(image, x - 11, y + 6, 2, 2, WindowGold);
		FillRect(image, x + 14, y + 12, 2, 2, WindowGold);
	}

	private static void DrawFlame(Image image, int x, int y, int height)
	{
		FillRect(image, x - 9, y + height - 14, 18, 14, FireRed);
		FillRect(image, x - 7, y + 7, 14, height - 20, FireOrange);
		FillRect(image, x - 4, y, 7, 14, FireOrange);
		FillRect(image, x + 4, y + 5, 5, 11, FireRed);
		FillRect(image, x - 2, y + 9, 5, height - 23, FireYellow);
		FillRect(image, x - 5, y + height - 9, 10, 5, FireYellow);
	}

	private static void DrawSmoke(Image image, int x, int y)
	{
		FillCircle(image, x + 15, y + 25, 12, Smoke);
		FillCircle(image, x + 29, y + 13, 15, StoneShadow);
		FillCircle(image, x + 44, y + 2, 13, Smoke);
		FillRect(image, x + 12, y + 20, 35, 12, Smoke);
	}

	private static void DrawStars(Image image)
	{
		int[] xs = { 19, 53, 95, 143, 197, 234, 295, 42, 274, 126, 219, 72 };
		int[] ys = { 24, 62, 38, 18, 47, 27, 81, 101, 103, 71, 89, 30 };
		for (int i = 0; i < xs.Length; i++)
		{
			SetPixel(image, xs[i], ys[i], Star);
			if (i % 3 == 0)
				SetPixel(image, xs[i] + 1, ys[i], Star);
		}
	}

	private static void DrawEmbers(Image image)
	{
		for (int i = 0; i < 34; i++)
		{
			int x = (i * 47 + 17) % FrameWidth;
			int y = (i * 29 + 8) % 125;
			SetPixel(image, x, y, i % 3 == 0 ? FireYellow : FireOrange);
			if (i % 4 == 0)
				SetPixel(image, x + 1, y, FireRed);
		}
	}

	private static void DrawPixelHeart(Image image, int x, int y)
	{
		FillRect(image, x, y, 3, 3, Red);
		FillRect(image, x + 5, y, 3, 3, Red);
		FillRect(image, x - 1, y + 2, 10, 3, Red);
		FillRect(image, x + 1, y + 5, 6, 3, Red);
		FillRect(image, x + 3, y + 8, 2, 2, Red);
	}

	private static void DrawPixelSword(Image image, int x, int y)
	{
		DrawLine(image, x, y + 12, x + 17, y - 5, StoneHighlight);
		DrawLine(image, x - 3, y + 10, x + 4, y + 17, Ink);
		FillRect(image, x - 6, y + 14, 8, 3, WindowGold);
	}

	private static void TileRect(Image destination, Image atlas, int x, int y, int width, int height, int tileX, int tileY)
	{
		if (atlas == null)
			return;
		Rect2I source = new Rect2I(tileX, tileY, TileSize, TileSize);
		for (int py = y; py < y + height; py += TileSize)
		{
			for (int px = x; px < x + width; px += TileSize)
			{
				int copyWidth = Mathf.Min(TileSize, x + width - px);
				int copyHeight = Mathf.Min(TileSize, y + height - py);
				destination.BlitRectMask(atlas, atlas,
					new Rect2I(source.Position, new Vector2I(copyWidth, copyHeight)), new Vector2I(px, py));
			}
		}
	}

	private static void DrawSprite(Image destination, Image sprite, Vector2I position, bool flipX = false)
	{
		if (sprite == null)
			return;
		Image drawImage = Image.CreateEmpty(sprite.GetWidth(), sprite.GetHeight(), true, Image.Format.Rgba8);
		drawImage.BlitRect(sprite, new Rect2I(0, 0, sprite.GetWidth(), sprite.GetHeight()), Vector2I.Zero);
		if (flipX)
			drawImage.FlipX();
		destination.BlitRectMask(drawImage, drawImage,
			new Rect2I(0, 0, drawImage.GetWidth(), drawImage.GetHeight()), position);
	}

	private static void FillRect(Image image, int x, int y, int width, int height, Color color)
	{
		int left = Mathf.Clamp(x, 0, FrameWidth);
		int top = Mathf.Clamp(y, 0, FrameHeight);
		int right = Mathf.Clamp(x + width, 0, FrameWidth);
		int bottom = Mathf.Clamp(y + height, 0, FrameHeight);
		for (int py = top; py < bottom; py++)
			for (int px = left; px < right; px++)
				image.SetPixel(px, py, color);
	}

	private static void SetPixel(Image image, int x, int y, Color color)
	{
		if (x >= 0 && x < FrameWidth && y >= 0 && y < FrameHeight)
			image.SetPixel(x, y, color);
	}

	private static void FillCircle(Image image, int centerX, int centerY, int radius, Color color)
	{
		for (int y = -radius; y <= radius; y++)
			for (int x = -radius; x <= radius; x++)
				if (x * x + y * y <= radius * radius)
					SetPixel(image, centerX + x, centerY + y, color);
	}

	private static void DrawLine(Image image, int x0, int y0, int x1, int y1, Color color)
	{
		int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
		int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
		int error = dx + dy;
		while (true)
		{
			SetPixel(image, x0, y0, color);
			if (x0 == x1 && y0 == y1)
				break;
			int twiceError = 2 * error;
			if (twiceError >= dy) { error += dy; x0 += sx; }
			if (twiceError <= dx) { error += dx; y0 += sy; }
		}
	}

	private static Color Hex(string value) => Color.FromString($"#{value}", Colors.Magenta);
}
