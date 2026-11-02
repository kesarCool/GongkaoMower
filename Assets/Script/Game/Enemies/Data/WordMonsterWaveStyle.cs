using UnityEngine;

/// <summary>文字怪整波面色模式（由 <see cref="SpawnerWaves"/> 每波开始时设置）。</summary>
public enum WordMonsterWaveTintMode
{
    Off = 0,
    /// <summary>每波从地图配色中随机选一种字色</summary>
    RandomPerWave = 1,
    /// <summary>同关卡 ID + 同 wave 表字段可复现同色</summary>
    SeededByLevelAndWave = 2,
}

/// <summary>
/// 文字怪「整波统一底色」：<see cref="EnemyWordLabel"/> 读取 <see cref="HasWaveTint"/> 后只做微小 per-enemy 色相抖动。
/// </summary>
public static class WordMonsterWaveStyle
{
    // Light faces are separated from both terrain and props by a solid dark outline.
    private static readonly Color[] Palette = {
        new Color32(255,245,221,255),new Color32(165,240,218,255),new Color32(184,223,254,255),
        new Color32(255,219,122,255),new Color32(255,198,161,255)
    };
    public static bool HasWaveTint { get; private set; }

    /// <summary>当前波字面渐变用的「面」色基准（不含描边）。</summary>
    public static Color WaveFaceTint { get; private set; }

    public static void ClearWaveTint()
    {
        HasWaveTint = false;
    }

    public static void ApplyWaveStart(WordMonsterWaveTintMode mode, int levelId, int waveNumber)
    {
        if (mode == WordMonsterWaveTintMode.Off)
        {
            ClearWaveTint();
            return;
        }

        if (mode == WordMonsterWaveTintMode.RandomPerWave)
        {
            WaveFaceTint = Palette[Random.Range(0,Palette.Length)];
            HasWaveTint = true;
            return;
        }

        unchecked
        {
            int seed = levelId * 73856093 ^ waveNumber * 19349663;
            var rng = new System.Random(seed);
            WaveFaceTint = Palette[rng.Next(Palette.Length)];
            HasWaveTint = true;
        }
    }
}
