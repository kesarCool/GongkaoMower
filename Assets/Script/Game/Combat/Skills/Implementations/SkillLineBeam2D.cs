using UnityEngine;

/// <summary>
/// 射线/线段技能：从玩家指向最近敌人发射一条 2D 线段，命中多个敌人并造成伤害
/// </summary>
public class SkillLineBeam2D : SkillBase
{
    public float beamLength = 8f;
    public float damage = 2f;
    public float interval = 0.8f;
    public LayerMask hitMask;
    public int beamCount = 1;
    public float spreadDegrees = 14f;

    [Header("可视化（可选）")]
    [Tooltip("用于在 Game 视图显示射线段的 LineRenderer（由 PlayerSkills 自动创建并注入）")]
    public LineRenderer beamLine;

    [Tooltip("射线可视化持续时间（秒）。每次触发伤害判定时刷新显示窗口。")]
    public float visualDuration = 0.08f;

    private float _timer;
    private float _visualUntil;

    public SkillLineBeam2D(float beamLength, float damage, float interval, LayerMask hitMask)
    {
        Id = SkillId.LineBeam;
        this.beamLength = Mathf.Max(0.5f, beamLength);
        this.damage = Mathf.Max(0.01f, damage);
        this.interval = Mathf.Max(0.05f, interval);
        this.hitMask = hitMask;
    }

    /// <summary>
    /// 由外部（例如 PlayerSkills）注入 LineRenderer，用于可视化射线。
    /// </summary>
    public void SetBeamLine(LineRenderer lr)
    {
        beamLine = lr;
    }

    public override void Tick(float deltaTime)
    {
        if (!_equipped) return;
        if (_ctx.player == null) return;

        _timer += deltaTime;
        if (_timer < interval) return;
        _timer = 0f;

        Vector2 origin = _ctx.player.position;
        Vector2 dir = Vector2.right;

        if (!string.IsNullOrEmpty(_ctx.enemyTag))
        {
            GameObject enemy = FindNearestEnemy(origin, _ctx.enemyTag);
            if (enemy != null)
            {
                Vector2 d = (Vector2)enemy.transform.position - origin;
                if (d.sqrMagnitude > 0.0001f) dir = d.normalized;
            }
        }

        int count = Mathf.Max(1, beamCount);
        float total = Mathf.Max(0f, spreadDegrees);
        float step = count <= 1 ? 0f : total / (count - 1);
        float start = -total * 0.5f;

        // 可视化：先画中间那条（体验更直观），其余射线纯判定
        UpdateBeamVisual(origin, dir);

        // 很多 2D 敌人会用 Trigger Collider；默认 Raycast 可能忽略 Trigger，这里临时开启命中 Trigger
        bool prev = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;

        for (int i = 0; i < count; i++)
        {
            float ang = start + step * i;
            Vector2 d = Quaternion.Euler(0f, 0f, ang) * dir;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, d, beamLength, hitMask);

            for (int h = 0; h < hits.Length; h++)
            {
                Collider2D col = hits[h].collider;
                if (col == null) continue;

                EnemyBase eb = col.GetComponent<EnemyBase>();
                if (eb == null) eb = col.GetComponentInParent<EnemyBase>();
                if (eb == null) continue;

                eb.TakeDamage(damage, SkillId.LineBeam);
            }
        }

        Physics2D.queriesHitTriggers = prev;
    }

    private void UpdateBeamVisual(Vector2 origin, Vector2 dir)
    {
        if (beamLine == null) return;

        Vector3 a = new Vector3(origin.x, origin.y, 0f);
        Vector3 b = a + (Vector3)(dir * beamLength);

        beamLine.positionCount = 2;
        beamLine.SetPosition(0, a);
        beamLine.SetPosition(1, b);
        beamLine.enabled = true;

        _visualUntil = Time.time + Mathf.Max(0.01f, visualDuration);
    }

    /// <summary>
    /// 由 PlayerSkills.Update 调用：到时间后隐藏线段，避免一直显示。
    /// </summary>
    public void TickVisual()
    {
        if (beamLine == null) return;
        if (!beamLine.enabled) return;
        if (Time.time >= _visualUntil)
            beamLine.enabled = false;
    }
}
