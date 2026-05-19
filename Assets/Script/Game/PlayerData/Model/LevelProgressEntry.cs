using System;

/// <summary>单关进度（键为配表 <c>levelId</c>，如 101、102）。</summary>
[Serializable]
public class LevelProgressEntry
{
    public int levelId;
    public int stars;
    public float bestTimeSec;
    public int bestKills;
    public bool cleared;
}
