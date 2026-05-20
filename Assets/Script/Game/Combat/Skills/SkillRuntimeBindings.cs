using System;
using UnityEngine;

/// <summary>
/// Player 侧技能运行时钩子与 Prefab 覆盖（P1 前）；数值由 SkillDef 驱动。
/// </summary>
public sealed class SkillRuntimeBindings
{
    // LineBeam 表现（挂 Player 上）
    public float beamVisualDuration = 0.08f;
    public Action<SkillLineBeam2D> configureLineBeam;

    // OrbitingBlades Prefab 覆盖（P1 迁入 Def）
    public GameObject bladePrefab;
    public Sprite bladeSprite;
    public int bladeSpriteSortingOrder = 50;
    public Color bladeSpriteTint = Color.white;
    public float bladeVisualScale = 1f;

    // ThrowGrenade Prefab 覆盖（P1 迁入 Def）
    public GameObject grenadePrefab;
    public GameObject grenadeExplosionFxPrefab;
}
