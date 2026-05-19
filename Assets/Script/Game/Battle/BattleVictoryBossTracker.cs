/// <summary>最后一波 Boss 存活计数：生成时 +1，击杀时 -1，归零即满足 Boss 通关条件。</summary>
public static class BattleVictoryBossTracker
{
    public static int BossesAlive { get; private set; }
    public static bool UsesBossVictory { get; private set; }

    public static void Reset()
    {
        BossesAlive = 0;
        UsesBossVictory = false;
    }

    public static void RegisterBossSpawned()
    {
        UsesBossVictory = true;
        BossesAlive++;
    }

    public static bool TryRegisterKill()
    {
        if (!UsesBossVictory || BossesAlive <= 0)
            return false;

        BossesAlive--;
        return BossesAlive <= 0;
    }

    public static bool IsBossVictoryReady => UsesBossVictory && BossesAlive <= 0;
}
