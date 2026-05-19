using System;

/// <summary>Guest 单存档槽 JSON 根对象。</summary>
[Serializable]
public class PlayerSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string playerId;
    public LevelProgressEntry[] levels = Array.Empty<LevelProgressEntry>();
}
