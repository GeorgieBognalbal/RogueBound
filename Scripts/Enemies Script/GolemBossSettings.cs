using Godot;

public static class GolemBossSettings
{
	public const string BOSS_NAME = "Galio";
	public const string BOSS_TITLE = "The Golem";

	public const float BOSS_BAR_WIDTH = 360.0f;
	public const float BOSS_BAR_HEIGHT = 18.0f;
	// The floor/room and status labels occupy the first ~90 logical pixels.
	public const float BOSS_BAR_TOP = 104.0f;
	public const float BOSS_BAR_ENTER_SECONDS = 0.24f;
	public const float BOSS_BAR_GROUP_ENTER_OFFSET = 12.0f;
	public const float BOSS_BAR_TRAIL_DELAY_SECONDS = 0.35f;
	public const float BOSS_BAR_TRAIL_DRAIN_SECONDS = 0.9f;
	public const float BOSS_BAR_BORDER_SIZE = 2.0f;
	public const float BOSS_NAME_FONT_SIZE = 26.0f;
	public const float BOSS_TITLE_FONT_SIZE = 18.0f;
	public const float BOSS_NAME_LABEL_HEIGHT = 38.0f;
	public const float BOSS_TITLE_LABEL_HEIGHT = 28.0f;
	public const float BOSS_BAR_NAME_Y_OFFSET = 0.0f;
	public const float BOSS_BAR_TITLE_Y_OFFSET = 40.0f;
	public const int BOSS_NAME_OUTLINE_SIZE = 5;
	public const int BOSS_TITLE_OUTLINE_SIZE = 4;
	public const string BOSS_TEXT_COLOR = "#ffffff";
	public const string BOSS_TEXT_OUTLINE_COLOR = "#000000";
	public const string BOSS_BAR_TRACK_COLOR = "#17131f";
	public const string BOSS_BAR_FILL_COLOR = "#bd343f";
	public const string BOSS_BAR_TRAIL_COLOR = "#f0a36d";
	public const string BOSS_BAR_BORDER_COLOR = "#ffff00";
	public const string ROCK_WARNING_COLOR = "#ff8a20";
	public const string ROCK_WARNING_ALT_COLOR = "#d84c27";
	public const string ROCK_WARNING_CORE_COLOR = "#4a2522";

	public const float BOSS_ATTACK_RANGE = 250.0f;
	public const float BOSS_MOVE_SPEED = 18.0f;
	public const float BOSS_STOP_DISTANCE = 150.0f;
	public const float BOSS_BASE_HEALTH = 420.0f;
	public const float BOSS_BASE_CONTACT_DAMAGE = 18.0f;
	public const float BOSS_CONTACT_ATTACK_COOLDOWN = 0.0f;
	public const float SMALL_ROCK_DAMAGE = 12.0f;
	public const float BOULDER_DAMAGE = 24.0f;
	public const float SMALL_ROCK_DAMAGE_RATIO_ON_SCATTER = 0.65f;
	public const float SMALL_ROCK_ORIGIN_SIDE_OFFSET = 24.0f;
	public const float SMALL_ROCK_ORIGIN_HEIGHT = -58.0f;
	public const float BOULDER_ORIGIN_SIDE_OFFSET = 12.0f;
	public const float BOULDER_ORIGIN_HEIGHT = -68.0f;
	public const float SMALL_ROCK_MIN_FLIGHT_SECONDS = 0.45f;
	public const float SMALL_ROCK_MAX_FLIGHT_SECONDS = 2.4f;
	public const float BOULDER_MIN_FLIGHT_SECONDS = 0.55f;
	public const float BOULDER_MAX_FLIGHT_SECONDS = 2.6f;
	public const float BOSS_DEATH_HOLD_SECONDS = 3.0f;
	public const float BOULDER_RENDER_SCALE = 2.0f;
	public const int BARRAGE_LEFT_THROW_FRAME = 0;
	public const int BARRAGE_RIGHT_THROW_FRAME = 2;
	public const int BOULDER_THROW_FRAME = 3;
	public const float SMALL_ROCK_SPEED = 131.25f;
	public const float BOULDER_SPEED = 156.25f;
	public const float PEBBLE_SPEED = 187.5f;
	public const float BARRAGE_COOLDOWN = 3.375f;
	public const float BOULDER_COOLDOWN = 2.625f;
	public const float BARRAGE_DURATION = 3.6f;
	public const float ROCK_LANDING_WARNING_BASE_SECONDS = 0.8f;
	public const float ATTACK_SPEED_COOLDOWN_MULTIPLIER = 0.75f;
	public static readonly float ROCK_LANDING_WARNING_SECONDS = Mathf.Max(0.5f, ROCK_LANDING_WARNING_BASE_SECONDS * ATTACK_SPEED_COOLDOWN_MULTIPLIER);
	public const float SMALL_ROCK_GROUND_SECONDS = 2.4f;
	public const float ROCK_SWEEP_SAMPLE_PIXELS = 4.0f;
	public const int PEBBLE_COUNT = 8;
	public const float PEBBLE_FLIGHT_SECONDS = 0.55f;
	public const float ROCK_WARNING_RADIUS = 18.0f;
	public const float ROCK_WARNING_BLINK_HZ = 4.0f;
}
