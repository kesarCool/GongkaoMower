using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 免伤技能：Boss 开启护盾，持续期间按配置的伤害类型过滤减伤。
/// elementNum = "4,14,0.6,Physical|Energy,3" = duration, cooldown, resistRatio, blockedTypes, maxTriggers
/// </summary>
public class ResistModule : BossSkillModule
{
    private float _duration = 4f;
    private float _resistRatio = 0.6f;
    private SkillDamageType[] _blockedTypes = Array.Empty<SkillDamageType>();
    private int _maxTriggers = 3;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);
        firstDelayMul = 0.5f;

        float[] p = ParseFloats(rawParams, 5);
        _duration     = p[0] > 0f ? p[0] : 4f;
        interval      = p[1] > 0f ? p[1] : 14f;
        _resistRatio  = p[2] > 0f ? p[2] : 0.6f;
        _maxTriggers  = Mathf.Max(0, (int)(p[4] > 0f ? p[4] : 3f));
        cooldown      = interval * firstDelayMul;

        // p[3] 是 blockedTypes 索引，从 rawParams 的原始字符串按逗号拆分取第 4 段
        _blockedTypes = ParseBlockedTypes(rawParams);
    }

    public override void Execute()
    {
        ResetCooldown();

        // 先销毁旧盾（新盾覆盖）
        var old = boss.GetComponent<ResistShield>();
        if (old != null) UnityEngine.Object.Destroy(old);

        // ResistShield.Setup() 内部处理全部视觉：扩散光环 + Boss 缩放脉冲 + 持续蓝色护盾
        var shield = boss.gameObject.AddComponent<ResistShield>();
        shield.Setup(_resistRatio, _blockedTypes, _maxTriggers, _duration);

        // IsBusy 只持续一帧，不阻塞其他技能
        brain.IsBusy = false;
    }

    /// <summary>从原始参数字符串中解析 blockedTypes（用 | 分隔）。</summary>
    private static SkillDamageType[] ParseBlockedTypes(string rawParams)
    {
        if (string.IsNullOrWhiteSpace(rawParams)) return Array.Empty<SkillDamageType>();

        string[] parts = rawParams.Split(',');
        if (parts.Length < 4) return Array.Empty<SkillDamageType>();

        string typeStr = parts[3]?.Trim();
        if (string.IsNullOrEmpty(typeStr)) return Array.Empty<SkillDamageType>();

        string[] tokens = typeStr.Split('|');
        var list = new List<SkillDamageType>(tokens.Length);
        foreach (var token in tokens)
        {
            if (Enum.TryParse(token.Trim(), true, out SkillDamageType t))
                list.Add(t);
            else
                Debug.LogWarning($"[ResistModule] 未知伤害类型: '{token}'，已跳过");
        }
        return list.ToArray();
    }
}
