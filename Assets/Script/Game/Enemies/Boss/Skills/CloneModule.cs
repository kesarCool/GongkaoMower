using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 分身技能：Boss 生成自身的克隆体，半透明、低血量、限时存活。
/// elementNum = "8,0.4,2,15,homingKnife|dash" = cooldown, hpPercent, cloneCount, lifetime, inheritSkills
/// inheritSkills: "homingKnife|dash"=仅继承指定技能, "*"=全部, ""=无技能(白板)
/// 无论如何不会继承 "clone" 类型（防止套娃）。
/// </summary>
public class CloneModule : BossSkillModule
{
    private float _hpPercent = 0.4f;
    private int _cloneCount = 1;
    private float _lifetime = 12f;
    private float _chargeTime = 0.35f;
    private string _inheritSkillsRaw;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);
        firstDelayMul = 0.6f;

        float[] p = ParseFloats(rawParams, 5);
        interval       = p[0] > 0f ? p[0] : 8f;
        _hpPercent     = Mathf.Clamp01(p[1] > 0f ? p[1] : 0.4f);
        _cloneCount    = Mathf.Max(1, (int)(p[2] > 0f ? p[2] : 1f));
        _lifetime      = p[3] > 0f ? p[3] : 12f;
        cooldown       = interval * firstDelayMul;

        // 第 5 个参数是原始字符串的第 5 段（逗号分隔），不是浮点数
        _inheritSkillsRaw = ParseInheritSkills(rawParams);

        CacheSprites();
    }

    public override bool CanTrigger()
    {
        // 克隆体不能再分身
        if (boss.GetComponent<CloneMarker>() != null) return false;
        return base.CanTrigger();
    }

    public override void Execute()
    {
        ResetCooldown();
        brain.StartCoroutine(CloneRoutine());
    }

    private IEnumerator CloneRoutine()
    {
        brain.IsBusy = true;
        SetSpritesFlash(true, new Color(0.6f, 0.85f, 1f, 1f)); // 淡蓝——和护盾/复活区分
        yield return new WaitForSeconds(_chargeTime);
        SetSpritesFlash(false);

        EnemyBase originEb = boss.GetComponent<EnemyBase>();
        float cloneMaxHp = originEb != null ? originEb.MaxHp * _hpPercent : 10f;

        // 解析继承技能集（排除 clone 自身）
        var keepTypes = BuildInheritSet();

        // 环形分散
        float angleStep = _cloneCount > 1 ? 360f / _cloneCount : 0f;
        float baseAngle = Random.Range(0f, 360f);

        for (int i = 0; i < _cloneCount; i++)
        {
            float angle = (baseAngle + i * angleStep + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            float radius = Random.Range(2.5f, 4.5f);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector3 spawnPos = boss.position + new Vector3(offset.x, offset.y, 0f);

            GameObject clone = Object.Instantiate(boss.gameObject, spawnPos, boss.rotation);

            var cloneBrain = clone.GetComponent<BossBrain>();
            if (cloneBrain != null)
            {
                if (keepTypes.Count > 0)
                {
                    // 保留指定技能模块
                    cloneBrain.RemoveAllModulesExcept(keepTypes);
                }
                else
                {
                    // 无继承技能 → 摧毁 BossBrain，克隆体是纯白板
                    Object.Destroy(cloneBrain);
                }
            }

            // 移除护盾（如有，克隆体的 ResistShield 需要重新由 ResistModule 触发）
            var cloneShield = clone.GetComponent<ResistShield>();
            if (cloneShield != null) Object.Destroy(cloneShield);

            clone.AddComponent<CloneMarker>();

            var eb = clone.GetComponent<EnemyBase>();
            if (eb != null)
            {
                eb.ApplyTableStats(0, Mathf.CeilToInt(cloneMaxHp), 0);
                eb.ResetForPool();
            }

            // 半透明
            var sprites = clone.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in sprites)
            {
                Color c = sr.color;
                c.a = 0.55f;
                sr.color = c;
            }

            Object.Destroy(clone, _lifetime);
        }

        if (brain != null) brain.IsBusy = false;
    }

    private HashSet<string> BuildInheritSet()
    {
        var set = new HashSet<string>();
        string raw = _inheritSkillsRaw?.Trim();
        if (string.IsNullOrEmpty(raw)) return set;

        // "*" → 全部已知技能类型（clone 永远排除）
        if (raw == "*")
        {
            set.Add("homingKnife");
            set.Add("dash");
            set.Add("bladeBurst");
            set.Add("zone");
            set.Add("resist");
            set.Add("summon");
            set.Add("revive");
            return set;
        }

        string[] tokens = raw.Split('|');
        foreach (var t in tokens)
        {
            string trimmed = t.Trim();
            if (!string.IsNullOrEmpty(trimmed) && trimmed != "clone")
                set.Add(trimmed);
        }
        return set;
    }

    /// <summary>从 rawParams 的第 5 段提取 inheritSkills 字符串。</summary>
    private static string ParseInheritSkills(string rawParams)
    {
        if (string.IsNullOrWhiteSpace(rawParams)) return string.Empty;
        string[] parts = rawParams.Split(',');
        if (parts.Length < 5) return string.Empty;
        return parts[4]?.Trim() ?? string.Empty;
    }
}
