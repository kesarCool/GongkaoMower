using UnityEngine;

/// <summary>
/// Boss 存活计数。区分中间波 Boss 和最终波 Boss，
/// 使中间波 Boss 死亡推进波次，最终波 Boss 死亡触发胜利。
/// </summary>
public static class BattleVictoryBossTracker
{
    /// <summary>所有 Boss（中间 + 最终）中仍存活的。</summary>
    public static int BossesAlive { get; private set; }

    /// <summary>仅存活的最终波 Boss。</summary>
    public static int FinalBossesAlive { get; private set; }

    /// <summary>战场中使用过任何 Boss 机制时为 true。阻止非 Boss 胜利路径。</summary>
    public static bool UsesBossVictory { get; private set; }

    public static void Reset()
    {
        BossesAlive = 0;
        FinalBossesAlive = 0;
        UsesBossVictory = false;
    }

    /// <summary>Boss 生成时调用。</summary>
    /// <param name="isFinalBoss">最后一波 Boss 为 true（击杀 = 胜利）。</param>
    public static void RegisterBossSpawned(bool isFinalBoss)
    {
        UsesBossVictory = true;
        BossesAlive++;
        if (isFinalBoss)
            FinalBossesAlive++;
    }

    /// <summary>Boss 死亡时调用。</summary>
    /// <param name="isFinalBoss">与 RegisterBossSpawned 时相同的值。</param>
    /// <returns>包含 (wasKilled, waveComplete, victory) 的元组。调用方负责路由。</returns>
    public static (bool wasKilled, bool waveComplete, bool victory) RegisterBossKill(bool isFinalBoss)
    {
        if (!UsesBossVictory || BossesAlive <= 0)
            return (false, false, false);

        BossesAlive = Mathf.Max(0, BossesAlive - 1);
        bool victory = false;
        bool waveComplete = false;

        if (isFinalBoss)
        {
            FinalBossesAlive = Mathf.Max(0, FinalBossesAlive - 1);
            victory = FinalBossesAlive <= 0;
        }
        else
        {
            waveComplete = true; // 中间波 Boss 死亡总是完成其波次
        }

        return (true, waveComplete, victory);
    }

    /// <summary>所有 Boss 已死亡时为 true。向后兼容查询。</summary>
    public static bool IsBossVictoryReady => UsesBossVictory && BossesAlive <= 0;
}
