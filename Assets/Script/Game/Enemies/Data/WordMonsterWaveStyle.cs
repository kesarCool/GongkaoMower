using UnityEngine;

/// <summary>文字怪整波面色模式（由 <see cref="SpawnerWaves"/> 每波开始时设置）。</summary>
public enum WordMonsterWaveTintMode
{
    Off = 0,
    /// <summary>每波随机一次色相（饱和、亮度在易读区间）</summary>
    RandomPerWave = 1,
    /// <summary>同关卡 ID + 同 wave 表字段可复现同色</summary>
    SeededByLevelAndWave = 2,
}

/// <summary>
/// 文字怪「整波统一底色」：<see cref="EnemyWordLabel"/> 读取 <see cref="HasWaveTint"/> 后只做微小 per-enemy 色相抖动。
/// </summary>
public static class WordMonsterWaveStyle
{
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
            Color c = Random.ColorHSV(0f, 1f, 0.55f, 0.85f, 0.85f, 1f);
            c.a = 1f;
            WaveFaceTint = c;
            HasWaveTint = true;
            return;
        }

        unchecked
        {
            int seed = levelId * 73856093 ^ waveNumber * 19349663;
            var rng = new System.Random(seed);
            float h = (float)rng.NextDouble();
            float s = Mathf.Lerp(0.55f, 0.85f, (float)rng.NextDouble());
            float v = Mathf.Lerp(0.85f, 1f, (float)rng.NextDouble());
            Color c = Color.HSVToRGB(h, s, v);
            c.a = 1f;
            WaveFaceTint = c;
            HasWaveTint = true;
        }
    }
}
