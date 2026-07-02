using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 复活技能（被动）：Boss 死亡时文字炸开 → 延迟 → 重聚复活。
/// elementNum = "0.5,1.5,1" = reviveHpPercent, reviveDelay, maxRevives
/// </summary>
public class ReviveModule : BossSkillModule
{
    private float _reviveHpPercent = 0.5f;
    private float _reviveDelay = 1.5f;
    private int _maxRevives = 1;
    private int _reviveCount;
    private float _invincibleDuration = 0.5f;

    private EnemyBase _eb;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);

        float[] p = ParseFloats(rawParams, 3);
        _reviveHpPercent = Mathf.Clamp01(p[0] > 0f ? p[0] : 0.5f);
        _reviveDelay     = Mathf.Max(0.1f, p[1] > 0f ? p[1] : 1.5f);
        _maxRevives      = Mathf.Max(1, (int)(p[2] > 0f ? p[2] : 1f));

        _eb = boss.GetComponent<EnemyBase>();
        if (_eb != null)
            _eb.OnDied.AddListener(OnBossDied);
    }

    public override bool IsPassive => true;
    public override bool CanTrigger() => false;
    public override void Execute() { }

    private void OnBossDied()
    {
        if (_reviveCount >= _maxRevives) return;
        if (_eb == null) return;

        _reviveCount++;
        _eb.preventPoolDeath = true;
        brain.StartCoroutine(DeathAndReviveSequence());
    }

    private IEnumerator DeathAndReviveSequence()
    {
        Vector3 deathPos = boss.position;

        // ═══ 死亡视觉：文字炸开 ═══
        // 1. 闪红
        SetSpritesFlashTimed(new Color(1f, 0.15f, 0.15f, 1f), 0.15f);

        // 2. 文字碎片炸开（优先）或方块兜底
        SpawnDeathFragments(deathPos);

        // 3. 飘字
        SpawnFloatText(deathPos, "死亡!", new Color(1f, 0.2f, 0.2f, 1f), 2f);

        yield return null;
        // Boss 已被 HideForRevive 隐藏（含 MeshRenderer）

        // ═══ 复活等待 + 微光暗示 ═══
        float waited = 0f;
        while (waited < _reviveDelay && boss != null)
        {
            waited += Time.deltaTime;
            if (waited > _reviveDelay * 0.55f && boss != null)
            {
                // 间歇性蓝色粒子微光——暗示"要复活了"
                if (Mathf.PingPong(Time.time * 10f, 1f) < 0.25f)
                    SpawnGlowDot(deathPos, new Color(0.3f, 0.7f, 1f, 0.6f));
            }
            yield return null;
        }

        if (_eb == null || boss == null) yield break;

        // ═══ 复活：重聚 ═══
        float newHp = _eb.MaxHp * _reviveHpPercent;
        _eb.ApplyTableStats(0, Mathf.CeilToInt(newHp), 0);
        _eb.ResetForPool();
        _eb.ShowFromRevive();
        _eb.preventPoolDeath = false;

        Vector3 revivePos = boss.position;

        SpawnFlashCircle(revivePos);
        SpawnFloatText(revivePos, "复活!", new Color(0.2f, 0.8f, 1f, 1f), 2.5f);

        // Boss 弹性缩放弹入
        boss.localScale = Vector3.zero;
        float t2 = 0f;
        float springDur = 0.35f;
        while (t2 < springDur && boss != null)
        {
            t2 += Time.deltaTime;
            float u = t2 / springDur;
            float s = u < 0.6f
                ? Mathf.Lerp(0f, 1.35f, u / 0.6f)
                : Mathf.Lerp(1.35f, 1f, (u - 0.6f) / 0.4f);
            boss.localScale = Vector3.one * Mathf.Clamp(s, 0f, 1.5f);
            yield return null;
        }
        if (boss != null) boss.localScale = Vector3.one;

        // 短暂无敌
        if (boss == null || boss.gameObject == null) yield break;

        var shield = boss.gameObject.AddComponent<ResistShield>();
        shield.Setup(1f, new[] { SkillDamageType.Physical, SkillDamageType.Energy, SkillDamageType.Explosive }, 0, _invincibleDuration);

        Debug.Log($"[ReviveModule] Boss '{boss.name}' 复活 ({_reviveCount}/{_maxRevives})，HP={newHp:F0}");
    }

    // ══════════════════════════════════════════════
    //  死亡碎片
    // ══════════════════════════════════════════════

    private void SpawnDeathFragments(Vector3 center)
    {
        if (!TrySpawnTextFragments())
        {
            Debug.Log($"[ReviveModule] 文字碎片失败，回退方块碎片。Boss='{boss.name}'");
            SpawnSquareFragments(center);
        }
    }

    /// <summary>文字碎片：每个汉字拆成独立 TMP，沿随机方向飞散。</summary>
    private bool TrySpawnTextFragments()
    {
        // 优先走 EnemyWordLabel 官方 API
        var label = boss.GetComponentInChildren<EnemyWordLabel>();
        TextMeshPro tmp = null;
        if (label != null && label.TryGetWorldTextForShatter(out var wt))
            tmp = wt;
        if (tmp == null)
            tmp = ResolveWorldTMP(); // 兜底：自己找

        if (tmp == null)
        {
            Debug.LogWarning($"[ReviveModule] 未找到 TextMeshPro。Boss='{boss.name}'");
            return false;
        }

        tmp.ForceMeshUpdate();
        var textInfo = tmp.textInfo;
        if (textInfo.characterCount == 0)
        {
            Debug.LogWarning($"[ReviveModule] TMP text='{tmp.text}' characterCount=0，可能字体缺失或文本为空");
            return false;
        }

        // 收集可见字符
        var chars = new List<(Vector3 worldPos, char c)>();
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var ci = textInfo.characterInfo[i];
            if (!ci.isVisible) continue;
            Vector3 world = tmp.transform.TransformPoint((ci.topLeft + ci.bottomRight) * 0.5f);
            chars.Add((world, ci.character));
        }
        if (chars.Count == 0)
        {
            Debug.LogWarning($"[ReviveModule] TMP text='{tmp.text}' characterCount={textInfo.characterCount} 但无可见字符");
            return false;
        }

        // 隐藏原始文字
        var origMr = tmp.GetComponent<MeshRenderer>();
        if (origMr != null) origMr.enabled = false;

        var font = tmp.font;
        float fontSize = tmp.fontSize;
        bool useGradient = tmp.enableVertexGradient;
        var gradient = tmp.colorGradient;
        float outlineW = tmp.outlineWidth;
        Color outlineC = tmp.outlineColor;

        // EnemyWordLabel 顶点渐变模式下 color=white，真实颜色在 colorGradient
        Color faceColor = useGradient
            ? gradient.topLeft
            : tmp.color;

        Debug.Log($"[ReviveModule] 文字碎片源: TMP='{tmp.name}' text='{tmp.text}' gradient={useGradient} faceColor={faceColor} outline={outlineW:F2} fontSize={fontSize} font={font?.name ?? "null"}");

        foreach (var (worldPos, ch) in chars)
        {
            var frag = new GameObject("TextFrag");
            frag.transform.position = worldPos + Random.insideUnitSphere * 0.15f;

            var ft = frag.AddComponent<TextMeshPro>();
            ft.text = ch.ToString();
            ft.font = font;
            ft.fontSize = fontSize;
            ft.alignment = TextAlignmentOptions.Center;
            ft.fontStyle = FontStyles.Bold;

            // 还原顶点渐变 + 描边
            ft.enableVertexGradient = useGradient;
            if (useGradient)
            {
                ft.color = Color.white;
                ft.colorGradient = gradient;
            }
            else
            {
                ft.color = faceColor;
            }
            ft.outlineWidth = outlineW;
            ft.outlineColor = outlineC;

            var mr = frag.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 500;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(1.5f, 4.5f);
            float lifetime = Random.Range(0.55f, 0.95f);
            var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            brain.StartCoroutine(FlyAndFadeTMP(frag, ft, dir, speed, lifetime));
        }

        return true;
    }

    /// <summary>方块碎片兜底（无文字怪时用）。</summary>
    private void SpawnSquareFragments(Vector3 center)
    {
        int count = 12;
        for (int i = 0; i < count; i++)
        {
            var frag = new GameObject("DeathFrag");
            frag.transform.position = center + Random.insideUnitSphere * 0.3f;
            var sr = frag.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSquareSprite();
            sr.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
            sr.sortingOrder = 500;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(2f, 5f);
            float lifetime = Random.Range(0.5f, 0.9f);
            var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            brain.StartCoroutine(FlyAndFadeSprite(frag, sr, dir, speed, lifetime));
        }
    }

    private TextMeshPro ResolveWorldTMP()
    {
        // 从 Boss 子物体中找 TextMeshPro（EnemyWordLabel 的子节点上挂着世界空间 TMP）
        var allTmp = boss.GetComponentsInChildren<TextMeshPro>();
        if (allTmp.Length == 0)
        {
            Debug.LogWarning($"[ReviveModule] Boss '{boss.name}' 子物体无任何 TextMeshPro。");
            return null;
        }

        Debug.Log($"[ReviveModule] Boss '{boss.name}' 找到 {allTmp.Length} 个 TMP:");
        foreach (var t in allTmp)
            Debug.Log($"[ReviveModule]   TMP '{t.name}' text='{t.text}' color={t.color} enabled={t.enabled}");

        foreach (var t in allTmp)
        {
            if (!string.IsNullOrEmpty(t.text))
                return t;
        }
        return null;
    }

    // ══════════════════════════════════════════════
    //  动画协程
    // ══════════════════════════════════════════════

    private static IEnumerator FlyAndFadeTMP(GameObject go, TextMeshPro tmp, Vector3 dir, float speed, float lifetime)
    {
        float t = 0f;
        while (t < lifetime && go != null)
        {
            t += Time.deltaTime;
            float u = t / lifetime;
            go.transform.position += dir * (speed * Time.deltaTime * (1f - u * 0.7f));
            Color c = tmp.color;
            c.a = Mathf.Lerp(1f, 0f, u);
            tmp.color = c;
            yield return null;
        }
        if (go != null) Object.Destroy(go);
    }

    private static IEnumerator FlyAndFadeSprite(GameObject go, SpriteRenderer sr, Vector3 dir, float speed, float lifetime)
    {
        float t = 0f;
        while (t < lifetime && go != null)
        {
            t += Time.deltaTime;
            float u = t / lifetime;
            go.transform.position += dir * (speed * Time.deltaTime * (1f - u * 0.7f));
            Color c = sr.color;
            c.a = Mathf.Lerp(0.9f, 0f, u);
            sr.color = c;
            yield return null;
        }
        if (go != null) Object.Destroy(go);
    }

    // ══════════════════════════════════════════════
    //  光效 / 飘字 / 微光
    // ══════════════════════════════════════════════

    private static void SpawnFlashCircle(Vector3 center)
    {
        var go = new GameObject("ReviveFlash");
        go.transform.position = center;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(1f, 1f, 1f, 0.9f);
        sr.sortingOrder = 500;
        go.transform.localScale = Vector3.one * 0.1f;
        go.AddComponent<FlashCircleDriver>().StartFlash(sr, go);
    }

    /// <summary>微光粒子：死亡位置间歇闪烁，暗示即将复活。</summary>
    private static void SpawnGlowDot(Vector3 pos, Color color)
    {
        var go = new GameObject("GlowDot");
        go.transform.position = pos + Random.insideUnitSphere * 0.6f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = color;
        sr.sortingOrder = 499;
        go.transform.localScale = Vector3.one * 0.15f;

        // 快速膨胀+消失
        go.AddComponent<FlashCircleDriver>().StartFlash(sr, go);
    }

    private static void SpawnFloatText(Vector3 pos, string text, Color color, float riseSpeed)
    {
        BattleChineseFontRuntime.EnsureLoaded();
        var font = BattleChineseFontRuntime.LoadedFont;
        if (font == null) return;

        var go = new GameObject("ReviveText");
        go.transform.position = pos + Vector3.up * 0.5f;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 10f;
        tmp.font = font;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 500;

        go.AddComponent<FloatTextDriver>().StartFloat(riseSpeed, 0.8f);
    }

    public void OnBossDestroyed()
    {
        if (_eb != null)
            _eb.OnDied.RemoveListener(OnBossDied);
    }
}

/// <summary>闪爆圆圈驱动。</summary>
internal class FlashCircleDriver : MonoBehaviour
{
    public void StartFlash(SpriteRenderer sr, GameObject owner)
    {
        StartCoroutine(Run(sr, owner));
    }

    private IEnumerator Run(SpriteRenderer sr, GameObject owner)
    {
        float t = 0f;
        float dur = 0.4f;
        while (t < dur && owner != null)
        {
            t += Time.deltaTime;
            float u = t / dur;
            owner.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 4f, u);
            Color c = sr.color;
            c.a = Mathf.Lerp(0.9f, 0f, u);
            sr.color = c;
            yield return null;
        }
        if (owner != null) Destroy(owner);
    }
}

/// <summary>飘字驱动：上浮+淡出+自毁。</summary>
internal class FloatTextDriver : MonoBehaviour
{
    public void StartFloat(float riseSpeed, float lifetime)
    {
        StartCoroutine(Run(riseSpeed, lifetime));
    }

    private IEnumerator Run(float riseSpeed, float lifetime)
    {
        float t = 0f;
        var tmp = GetComponent<TextMeshPro>();
        while (t < lifetime)
        {
            t += Time.deltaTime;
            float u = t / lifetime;
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime * (1f - u));
            if (tmp != null) { var c = tmp.color; c.a = Mathf.Lerp(1f, 0f, u); tmp.color = c; }
            yield return null;
        }
        Destroy(gameObject);
    }
}
