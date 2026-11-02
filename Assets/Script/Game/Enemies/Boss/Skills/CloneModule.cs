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
    private List<GameObject> _clones = new List<GameObject>();
    private EnemyBase _trackedBossEb;
    private BossClonePresentation _presentation;
    private Coroutine _routine;
    private bool _disposed;
    private int _generation;
    public int TrackedCloneCount => _clones.FindAll(c => c != null).Count;

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
        _presentation = owner.GetComponentInChildren<BossClonePresentation>(true);
        if (_presentation != null) _presentation.EnableOwnerBadge();

        // 主身死亡 → 所有分身一并销毁
        _trackedBossEb = owner.GetComponent<EnemyBase>();
        if (_trackedBossEb != null)
            _trackedBossEb.OnDied.AddListener(OnMainBossDied);
    }

    /// <summary>主身死亡回调：真死（非复活假死）时销毁所有分身。</summary>
    private void OnMainBossDied()
    {
        // Revive may be a later OnDied listener. Resolve it after all listeners have run;
        // a real pool return is handled synchronously by BossBrain.OnDisable.
        if (brain != null && brain.gameObject.activeInHierarchy) brain.StartCoroutine(ResolveOwnerDeath());
    }
    private IEnumerator ResolveOwnerDeath()
    {
        yield return null;
        if (_trackedBossEb != null && _trackedBossEb.preventPoolDeath) yield break;
        ClearClones();
    }
    private void ClearClones()
    {
        for (int i = _clones.Count - 1; i >= 0; i--)
            if (_clones[i] != null)
            {
                var marker = _clones[i].GetComponent<CloneMarker>();
                if (marker != null) marker.Despawn(false); else Object.Destroy(_clones[i]);
            }
        _clones.Clear();
    }
    public void OnOwnerDisposed()
    {
        if (_disposed) return; _disposed = true; _generation++;
        if (_routine != null && brain != null) brain.StopCoroutine(_routine);
        _routine = null; ClearClones();
        SetSpritesFlash(false);
        if (_presentation != null) _presentation.DisableOwnerBadge();
        if (_trackedBossEb != null)
        {
            _trackedBossEb.OnDied.RemoveListener(OnMainBossDied);
            _trackedBossEb = null;
        }
    }

    public override bool CanTrigger()
    {
        // 克隆体不能再分身
        if (_disposed || boss == null || boss.GetComponent<CloneMarker>() != null) return false;
        return base.CanTrigger();
    }

    public override void Execute()
    {
        if (_disposed || boss == null || boss.GetComponent<CloneMarker>() != null) return;
        ResetCooldown();
        _routine = brain.StartCoroutine(CloneRoutine());
    }

    private IEnumerator CloneRoutine()
    {
        brain.IsBusy = true;
        int generation = _generation;
        if (_presentation != null) _presentation.BeginCharge(_chargeTime);
        SetSpritesFlash(true, new Color(1f, .4f, .18f, 1f));
        yield return new WaitForSeconds(_chargeTime);
        SetSpritesFlash(false);

        if (_disposed || generation != _generation || boss == null || !boss.gameObject.activeInHierarchy ||
            (_trackedBossEb != null && _trackedBossEb.Hp <= 0))
        {
            if (_presentation != null) _presentation.CancelCharge();
            if (brain != null) brain.IsBusy = false;
            yield break;
        }
        if (_presentation != null) _presentation.ReleaseCharge();

        EnemyBase originEb = boss.GetComponent<EnemyBase>();
        float cloneMaxHp = originEb != null ? originEb.MaxHp * _hpPercent : 10f;

        // 解析继承技能集（排除 clone 自身）
        var keepTypes = BuildInheritSet();

        // 清理已销毁的克隆体引用（自然死亡/超时）
        _clones.RemoveAll(c => c == null);

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
            clone.SetActive(false);
            var marker = clone.GetComponent<CloneMarker>();
            if (marker == null) marker = clone.AddComponent<CloneMarker>();
            marker.Configure(originEb, keepTypes, _lifetime);

            // 移除护盾（如有，克隆体的 ResistShield 需要重新由 ResistModule 触发）
            var cloneShield = clone.GetComponent<ResistShield>();
            if (cloneShield != null) Object.Destroy(cloneShield);

            // 移除 Boss 标记：克隆体不是 Boss，死亡不应触发胜利/推进波次
            var cloneBossMarker = clone.GetComponent<LastWaveBossMarker>();
            if (cloneBossMarker != null) Object.Destroy(cloneBossMarker);

            var eb = clone.GetComponent<EnemyBase>();
            if (eb != null)
            {
                eb.ApplyTableStats(0, Mathf.CeilToInt(cloneMaxHp), 0);
                eb.ResetForPool();
                eb.addKillToGameLayer = false;
            }

            // Legacy Sprite fallback; the new presenter applies opacity to actual TMP too.
            var sprites = clone.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in sprites)
            {
                Color c = sr.color;
                c.a = 0.55f;
                sr.color = c;
            }

            _clones.Add(clone);
            clone.SetActive(true);
        }

        if (brain != null) brain.IsBusy = false;
        _routine = null;
    }

    private HashSet<string> BuildInheritSet()
    {
        var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        string raw = _inheritSkillsRaw?.Trim();
        if (string.IsNullOrEmpty(raw)) return set;

        // "*" → 全部已知技能类型（clone 和 revive 永远排除）
        if (raw == "*")
        {
            set.Add("homingKnife");
            set.Add("dash");
            set.Add("bladeBurst");
            set.Add("zone");
            set.Add("resist");
            set.Add("summon");
            return set;
        }

        string[] tokens = raw.Split('|');
        foreach (var t in tokens)
        {
            string trimmed = t.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !string.Equals(trimmed,"clone",System.StringComparison.OrdinalIgnoreCase) && !string.Equals(trimmed,"revive",System.StringComparison.OrdinalIgnoreCase))
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
