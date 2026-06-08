using System;

/// <summary>单个英雄的升级进度（存入 PlayerSaveData）。</summary>
[Serializable]
public class HeroUpgradeEntry
{
    public string characterId;
    [UnityEngine.Tooltip("当前等级，1~max（未升级过 = 1）。")]
    public int level = 1;
    [UnityEngine.Tooltip("阶位：0=Normal, 1=Rare, 2=Legend。")]
    public int stage;
}
