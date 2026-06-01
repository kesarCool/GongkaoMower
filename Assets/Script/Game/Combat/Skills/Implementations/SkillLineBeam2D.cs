using UnityEngine;

/// <summary>
/// 射线技能：感知范围（beamLength）内仅 1 只怪时全部射线指向该怪；
/// 多只怪时第 1 条（红）指向范围内最近怪，其余 360° 随机散射。
/// </summary>
public class SkillLineBeam2D : SkillBase
{
    private static readonly Color[] RainbowBeamColors =
    {
        new Color(1f, 0.2f, 0.2f, 0.95f),
        new Color(1f, 0.55f, 0.1f, 0.95f),
        new Color(1f, 0.92f, 0.15f, 0.95f),
        new Color(0.25f, 0.95f, 0.35f, 0.95f),
        new Color(0.2f, 0.95f, 0.95f, 0.95f),
        new Color(0.3f, 0.5f, 1f, 0.95f),
        new Color(0.72f, 0.28f, 1f, 0.95f),
    };

    /// <summary>仅 1 只怪时多射线的散布角度（度）。0=全部重叠，10=左右各 5° 扇形。</summary>
    public float singleTargetSpreadDeg = 10f;

    public float beamLength = 8f;
    public float damage = 2f;
    public float interval = 0.8f;
    public LayerMask hitMask;
    public int beamCount = 1;
    public int maxLevelVariantInterval = 5;
    public int variantBeamCount = 7;
    public int skillMaxLevel = 5;

    [Header("可视化（可选）")]
    public LineRenderer[] beamLines;
    public float visualDuration = 0.08f;

    private float _timer;
    private float _visualUntil;
    private int _shotCounter;

    public SkillLineBeam2D(float beamLength, float damage, float interval, LayerMask hitMask)
    {
        Id = SkillId.LineBeam;
        this.beamLength = Mathf.Max(0.5f, beamLength);
        this.damage = Mathf.Max(0.01f, damage);
        this.interval = Mathf.Max(0.05f, interval);
        this.hitMask = hitMask;
    }

    public void SetBeamLines(LineRenderer[] lines)
    {
        beamLines = lines;
    }

    public static Color GetRainbowBeamColor(int beamIndex)
    {
        if (RainbowBeamColors.Length == 0)
            return Color.white;
        return RainbowBeamColors[Mathf.Abs(beamIndex) % RainbowBeamColors.Length];
    }

    public override void Tick(float deltaTime)
    {
        if (!_equipped) return;
        if (_ctx.player == null) return;

        _timer += deltaTime;
        if (_timer < interval) return;

        Vector2 origin = _ctx.player.position;
        float senseRange = Mathf.Max(0.5f, beamLength);

        if (string.IsNullOrEmpty(_ctx.enemyTag))
            return;

        int livingEnemies = CountActiveEnemiesInRange(origin, _ctx.enemyTag, senseRange);
        if (livingEnemies <= 0)
            return;

        _timer = 0f;

        _shotCounter++;
        bool useVariant = Level >= skillMaxLevel && maxLevelVariantInterval > 0
            && _shotCounter % maxLevelVariantInterval == 0;
        int count = Mathf.Max(1, useVariant ? variantBeamCount : beamCount);
        GameObject nearest = FindNearestEnemy(origin, _ctx.enemyTag, senseRange);
        Vector2 aimDir = Vector2.right;
        if (nearest != null)
        {
            Vector2 toEnemy = (Vector2)nearest.transform.position - origin;
            if (toEnemy.sqrMagnitude > 0.0001f)
                aimDir = toEnemy.normalized;
        }

        // 感知范围内仅 1 只：全部集中；否则第 0 条瞄最近，其余 360° 随机
        bool allAimAtMonster = livingEnemies == 1 && nearest != null;

        // 一次释放的暴击判定共享
        float finalDamage = GetFinalDamage(damage, out bool isCrit);

        bool prev = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;

        for (int i = 0; i < count; i++)
        {
            Vector2 d = ResolveBeamDirection(i, count, allAimAtMonster, nearest != null, aimDir);
            UpdateBeamVisualLine(i, origin, d);
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, d, beamLength, hitMask);

            for (int h = 0; h < hits.Length; h++)
            {
                Collider2D col = hits[h].collider;
                if (col == null) continue;

                EnemyBase eb = col.GetComponent<EnemyBase>();
                if (eb == null) eb = col.GetComponentInParent<EnemyBase>();
                if (eb == null) continue;

                eb.TakeDamage(finalDamage, SkillId.LineBeam, isCrit);
            }
        }

        Physics2D.queriesHitTriggers = prev;
        HideExtraBeamVisuals(count);
        PublishSkillCast(origin);
    }

    private Vector2 ResolveBeamDirection(int beamIndex, int totalBeams, bool allAimAtMonster, bool hasNearest, Vector2 aimDir)
    {
        if (allAimAtMonster)
        {
            // 仅 1 只怪：扇形散开，总角度 singleTargetSpreadDeg，中心对准目标
            if (totalBeams <= 1 || singleTargetSpreadDeg <= 0.01f)
                return aimDir;

            float halfSpread = singleTargetSpreadDeg * 0.5f;
            float t = totalBeams > 1 ? (float)beamIndex / (totalBeams - 1f) : 0.5f;
            float angleOffset = Mathf.Lerp(-halfSpread, halfSpread, t);
            return Quaternion.Euler(0f, 0f, angleOffset) * aimDir;
        }

        if (beamIndex == 0 && hasNearest)
            return aimDir;

        return RandomDirection2D();
    }

    private void UpdateBeamVisualLine(int index, Vector2 origin, Vector2 dir)
    {
        if (beamLines == null || index < 0 || index >= beamLines.Length)
            return;

        LineRenderer lr = beamLines[index];
        if (lr == null)
            return;

        Color c = GetRainbowBeamColor(index);
        lr.startColor = c;
        lr.endColor = c;

        Vector3 a = new Vector3(origin.x, origin.y, 0f);
        Vector3 b = a + (Vector3)(dir.normalized * beamLength);

        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.enabled = true;

        _visualUntil = Time.time + Mathf.Max(0.01f, visualDuration);
    }

    private void HideExtraBeamVisuals(int activeCount)
    {
        if (beamLines == null)
            return;

        for (int i = activeCount; i < beamLines.Length; i++)
        {
            if (beamLines[i] != null)
                beamLines[i].enabled = false;
        }
    }

    public void TickVisual()
    {
        if (beamLines == null)
            return;
        if (Time.time < _visualUntil)
            return;

        for (int i = 0; i < beamLines.Length; i++)
        {
            if (beamLines[i] != null)
                beamLines[i].enabled = false;
        }
    }
}
