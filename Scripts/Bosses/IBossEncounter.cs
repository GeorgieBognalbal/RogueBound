using Godot;

public interface IBossEncounter
{
	BossConfig BossConfig { get; }
	bool IsFightStarted { get; }
	void StartBossIntroOnce(Player player);
	void ShowBossBarIfFightStarted();
	void SetRoomActive(bool active);
	void HideBossBar();
	void CleanupBossEffects();
}
