using UnityEngine;

/// <summary><see cref="SkillId"/> → <see cref="AudioId"/>（技能施放音效）。</summary>
public static class SkillAudioMapping
{
    public static AudioId ToAudioId(SkillId skillId)
    {
        switch (skillId)
        {
            case SkillId.AutoProjectile: return AudioId.SkillAutoProjectile;
            case SkillId.LineBeam: return AudioId.SkillLineBeam;
            case SkillId.OrbitingBlades: return AudioId.SkillOrbitingBlades;
            case SkillId.ThrowGrenade: return AudioId.SkillThrowGrenade;
            case SkillId.FieldGenerator: return AudioId.SkillFieldGenerator;
            case SkillId.LightningStrike: return AudioId.SkillLightningStrike;
            default: return AudioId.None;
        }
    }
}
