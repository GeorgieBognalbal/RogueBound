using Godot;

public static class BossSpriteFramesBuilder
{
	public static SpriteFrames FromConfig(BossConfig config)
	{
		var sheet = GD.Load<Texture2D>(config.SpriteSheetPath);
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");
		foreach (BossAnimationRow row in config.Animations)
		{
			frames.AddAnimation(row.Name);
			float animationFps = config.Kind == BossKind.OrcKing
				? BossRoster.ORC_KING_ANIMATION_FPS
				: BossRoster.ANIMATION_FPS;
			frames.SetAnimationSpeed(row.Name, animationFps);
			frames.SetAnimationLoopMode(row.Name, row.Loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
			for (int frame = 0; frame < row.FrameCount; frame++)
			{
				var atlas = new AtlasTexture
				{
					Atlas = sheet,
					Region = new Rect2(frame * config.FrameSize, row.Row * config.FrameSize, config.FrameSize, config.FrameSize),
					FilterClip = true
				};
				frames.AddFrame(row.Name, atlas);
			}
		}
		return frames;
	}

	public static SpriteFrames ProjectileFrames()
	{
		var sheet = GD.Load<Texture2D>(BossRoster.IVERN_PROJECTILE_SHEET);
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");
		(string Name, int Count, bool Loop)[] rows =
		{
			("vine_straight", 2, true), ("vine_diag", 2, true), ("vine_fade", 3, false),
			("root_wrap", 4, false), ("root_break", 3, false), ("poison_fly", 2, true),
			("poison_land", 3, false), ("poison_ground", 2, true), ("poison_fade", 3, false),
		};
		for (int row = 0; row < rows.Length; row++)
		{
			var item = rows[row];
			frames.AddAnimation(item.Name);
			frames.SetAnimationSpeed(item.Name, BossRoster.ANIMATION_FPS);
			frames.SetAnimationLoopMode(item.Name, item.Loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
			for (int frame = 0; frame < item.Count; frame++)
			{
				frames.AddFrame(item.Name, new AtlasTexture
				{
					Atlas = sheet,
					Region = new Rect2(frame * BossRoster.PROJECTILE_CELL_SIZE, row * BossRoster.PROJECTILE_CELL_SIZE,
						BossRoster.PROJECTILE_CELL_SIZE, BossRoster.PROJECTILE_CELL_SIZE),
					FilterClip = true
				});
			}
		}
		return frames;
	}
}
