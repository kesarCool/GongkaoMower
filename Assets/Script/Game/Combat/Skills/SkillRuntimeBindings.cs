using System;

/// <summary>
/// Player 侧技能运行时钩子（仅 LineBeam 射线表现）；Prefab 与数值均由 SkillDef 驱动。
/// </summary>
public sealed class SkillRuntimeBindings
{
    public float beamVisualDuration = 0.08f;
    public Action<SkillLineBeam2D> configureLineBeam;
}
