using System;

/// <summary>单条成就组的玩家进度（存入 PlayerSaveData）。</summary>
[Serializable]
public class AchievementProgressEntry
{
    /// <summary>对应 AchievementConfig.GroupId。</summary>
    public int groupId;

    /// <summary>累计进度值（终生击杀数 / 累积钻石收入等）。</summary>
    public int currentValue;

    /// <summary>已领取的最高阶段序号（0 = 尚未领取任何阶段）。</summary>
    public int claimedStage;
}
