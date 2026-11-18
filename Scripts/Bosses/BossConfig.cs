using Godot;
using System.Collections.Generic;

public enum BossKind { Galio, Ivern, OrcKing }

public sealed class BossAnimationRow
{
	public string Name { get; }
	public int Row { get; }
	public int FrameCount { get; }
	public bool Loop { get; }

	public BossAnimationRow(string name, int row, int frameCount, bool loop)
	{
		Name = name;
		Row = row;
		FrameCount = frameCount;
		Loop = loop;
	}
}

public sealed class BossConfig
{
	public BossKind Kind { get; }
	public string Name { get; }
	public string Title { get; }
	public string SpriteSheetPath { get; }
	public string PortraitPath { get; }
	public int FrameSize { get; }
	public float MaxHealth { get; }
	public Color BannerColor { get; }
	public Color BannerEdgeColor { get; }
	public IReadOnlyList<BossAnimationRow> Animations { get; }

	public BossConfig(BossKind kind, string name, string title, string sheet, string portrait, int frameSize,
		float maxHealth, Color banner, Color edge, IReadOnlyList<BossAnimationRow> animations)
	{
		Kind = kind;
		Name = name;
		Title = title;
		SpriteSheetPath = sheet;
		PortraitPath = portrait;
		FrameSize = frameSize;
		MaxHealth = maxHealth;
		BannerColor = banner;
		BannerEdgeColor = edge;
		Animations = animations;
	}
}

public static class BossRoster
{
	public const int TOTAL_FLOORS = 10;
	public static readonly IReadOnlyDictionary<int, BossKind> BOSS_FLOORS = new Dictionary<int, BossKind>
	{
		[3] = BossKind.Galio,
		[6] = BossKind.Ivern,
		[10] = BossKind.OrcKing,
	};

	public const string IVERN_NAME = "Ivern";
	public const string IVERN_TITLE = "The Guardian of the Forest";
	public const float IVERN_MAX_HP = 630.0f;
	public const string IVERN_SHEET = "res://Assets/Enemies/Ivern/ivern_spritesheet.png";
	public const string IVERN_PROJECTILE_SHEET = "res://Assets/Enemies/Ivern/ivern_projectiles_spritesheet.png";
	public const float IVERN_MOVE_SPEED = 0.0f;
	public const float IVERN_ATTACK_COOLDOWN = 2.25f;
	public const int IVERN_MAX_ATTACKS_IN_ROW = 2;
	public const float IVERN_CONTACT_DAMAGE = 12.0f;
	public const string ORC_KING_NAME = "Trundle";
	public const string ORC_KING_TITLE = "The Orc King";
	public const int ORC_KING_MAX_HP = 1000;
	public const float ORC_KING_MOVE_SPEED = 42.0f;
	public const float ORC_KING_BODY_WIDTH = 26.0f;
	public const float ORC_KING_BODY_HEIGHT = 38.0f;
	public const float ORC_KING_ATTACK_COOLDOWN = 1.0f;
	public const float ORC_KING_ANIMATION_FPS = 7.0f;
	public const float ORC_KING_SLASH_DAMAGE = 32.0f;
	public const float ORC_KING_SLASH_RANGE = 70.0f;
	public const float ORC_KING_SLASH_ARC_DEGREES = 110.0f;
	public const int ORC_KING_SLASH_HIT_FRAME = 4;
	public const float ORC_KING_SMASH_DAMAGE = 42.0f;
	public const float ORC_KING_SMASH_COOLDOWN = 3.5f;
	public const float ORC_KING_SMASH_STUN_SECONDS = 1.0f;
	public const float ORC_KING_SMASH_TELEGRAPH_SECONDS = 1.0f;
	public const int ORC_KING_SMASH_HIT_FRAME = 7;
	public const float ORC_KING_SMASH_TELEGRAPH_WIDTH = 112.0f;
	public const float ORC_KING_SMASH_TELEGRAPH_HEIGHT = 56.0f;
	public const float ORC_KING_SUMMON_COOLDOWN = 12.0f;
	public const int ORC_KING_SUMMON_COUNT = 2;
	public const int ORC_KING_SUMMON_CAP = 4;
	public const int ORC_KING_SUMMON_START_FRAME = 7;
	public const float ORC_KING_THROW_DAMAGE = 28.0f;
	public const float ORC_KING_THROW_COOLDOWN = 3.5f;
	public const float ORC_KING_THROW_SPEED = 210.0f;
	public const bool ORC_KING_AXE_RETURNS = false;
	public const float ORC_KING_AXE_LIFETIME = 4.0f;
	public const int ORC_KING_AXE_RELEASE_FRAME = 3;
	public const float ORC_KING_PHASE_TWO_THRESHOLD = 0.5f;
	public const float ORC_KING_PHASE_TWO_SPEED_MULTIPLIER = 1.2f;
	public const float ORC_KING_PHASE_TWO_COOLDOWN_MULTIPLIER = 0.8f;
	public const float ORC_KING_RECOVERY_SECONDS = 0.25f;
	public const float ORC_KING_DEATH_HOLD_SECONDS = 1.0f;
	public const float ORC_KING_DEATH_ANIMATION_SECONDS = 2.0f;
	public const float ORC_KING_PREINTRO_DELAY_SECONDS = 0.1f;
	public const int VINE_COUNT = 8;
	public const float VINE_ROTATION_JITTER_DEGREES = 45.0f;
	public const int VINE_BURST_FRAME = 1;
	public const float VINE_SPEED = 164.0625f;
	public const float VINE_MAX_RANGE = 430.0f;
	public const float VINE_DAMAGE = 0.0f;
	public const float VINE_ORIGIN_Y = -5.0f;
	public const float ROOT_DURATION = 2.0f;
	public const float ROOT_IMMUNITY = 1.5f;
	public const float IVERN_VISUAL_SCALE = 2.0f;
	public const float IVERN_BODY_WIDTH = 20.0f;
	public const float IVERN_BODY_HEIGHT = 28.0f;
	public const float IVERN_BODY_CENTER_Y = -14.0f;
	public const float IVERN_DAMAGE_WIDTH = 24.0f;
	public const float IVERN_DAMAGE_HEIGHT = 30.0f;
	public const float IVERN_SPRITE_OFFSET_Y = -24.0f;
	public const float IVERN_HIT_FLASH_SECONDS = 0.12f;
	public const float POISON_SPEED = 195.3125f;
	public const float POISON_OFFSET_RADIUS = 64.0f;
	public const float POISON_ARC_HEIGHT = 56.0f;
	public const float POISON_ORIGIN_Y = -34.0f;
	public const int POISON_FIRST_SPIT_FRAME = 0;
	public const int POISON_SECOND_SPIT_FRAME = 3;
	public const float POISON_DURATION = 5.0f;
	public const float POISON_TICK_DAMAGE = 5.0f;
	public const float POISON_TICK_INTERVAL = 0.75f;
	public const int POISON_MAX_PUDDLES = 4;
	public const float POISON_PUDDLE_WIDTH = 14.0f;
	public const float POISON_PUDDLE_HEIGHT = 7.0f;
	public const float PLAYER_BODY_PADDING_X = 9.0f;
	public const float PLAYER_BODY_PADDING_Y = 9.0f;
	public const float VINE_STRAIGHT_HALF_LENGTH = 10.0f;
	public const float VINE_STRAIGHT_HALF_WIDTH = 2.5f;
	public const float VINE_DIAGONAL_HALF_LENGTH = 7.0f;
	public const float VINE_DIAGONAL_HALF_WIDTH = 7.0f;
	public const float POISON_BLOB_HALF_LENGTH = 5.0f;
	public const float POISON_BLOB_HALF_WIDTH = 4.0f;
	public const int POISON_BARRAGE_LOOPS = 3;
	public const uint PROJECTILE_COLLISION_MASK = 3;
	public const int BOSS_FRAME_SIZE = 48;
	public const int PROJECTILE_CELL_SIZE = 32;
	public const int ANIMATION_FPS = 5;
	public const float ANIMATION_FRAME_SECONDS = 0.2f;

	public static readonly BossConfig GALIO = new(
		BossKind.Galio, GolemBossSettings.BOSS_NAME, GolemBossSettings.BOSS_TITLE,
		"res://Assets/Enemies/Golem/golem_spritesheet_5fps.png", "res://Assets/Bosses/galio_intro_sprite.png", 48,
		GolemBossSettings.BOSS_BASE_HEALTH, new Color("a8541c"), new Color("ec963c"), new BossAnimationRow[]
		{
			new("spawn", 0, 10, false), new("idle", 1, 4, true), new("walk", 2, 6, true),
			new("attack_barrage", 3, 6, true), new("attack_boulder", 4, 7, false), new("death", 5, 9, false)
		});

	public static readonly BossConfig IVERN = new(
		BossKind.Ivern, IVERN_NAME, IVERN_TITLE, IVERN_SHEET, IVERN_SHEET, BOSS_FRAME_SIZE,
		IVERN_MAX_HP, new Color("2f783d"), new Color("91d36f"), new BossAnimationRow[]
		{
			new("spawn", 0, 10, false), new("idle", 1, 4, true), new("attack_vines", 2, 6, false),
			new("attack_poison", 3, 6, true), new("death", 4, 9, false)
		});

	public static readonly BossConfig ORC_KING = new(
		BossKind.OrcKing, ORC_KING_NAME, ORC_KING_TITLE, "res://Assets/Enemies/OrcKing/orc_king_spritesheet.png",
		"res://Assets/Enemies/OrcKing/orc_king_intro_sprite.png", 64, ORC_KING_MAX_HP, new Color("704225"), new Color("dab574"),
		new BossAnimationRow[] { new("spawn", 0, 8, false), new("idle", 1, 6, true), new("slash", 2, 8, false),
			new("smash", 3, 10, false), new("summon", 4, 8, false), new("throw", 5, 8, false), new("death", 6, 10, false) });

	public static BossConfig Get(BossKind kind) => kind switch
	{
		BossKind.Galio => GALIO,
		BossKind.Ivern => IVERN,
		_ => ORC_KING
	};

	public static bool TryGetFloorBoss(int floor, out BossKind kind) => BOSS_FLOORS.TryGetValue(floor, out kind);
}
