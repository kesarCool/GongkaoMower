using System;

/// <summary>Guest 单存档槽 JSON 根对象。</summary>
[Serializable]
public class PlayerSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public string playerId;
    public LevelProgressEntry[] levels = Array.Empty<LevelProgressEntry>();
    /// <summary>已上阵角色 ID（空 = 未选/使用默认）。</summary>
    public string equippedCharacterId;
}
