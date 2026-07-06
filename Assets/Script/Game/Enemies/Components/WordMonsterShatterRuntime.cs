using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 文字怪死亡碎片：全局活跃上限、每怪字数上限、多帧消化队列。
/// 场景中放一个实例；订阅 <see cref="EnemyDiedEvent"/>，在敌人回池前快照 TMP。
/// </summary>
[DisallowMultipleComponent]
public class WordMonsterShatterRuntime : MonoBehaviour
{
    public static WordMonsterShatterRuntime Instance { get; private set; }

    [Header("预算（清屏友好）")]
    [Tooltip("同时存在的字碎片数量上限")]
    [SerializeField] private int maxActiveFragments = 64;

    [Tooltip("每只怪最多入队几个字（其余丢弃）")]
    [SerializeField] private int maxFragmentsPerEnemy = 4;

    [Tooltip("把「新碎片生成」分摊到多少帧（每帧上限 ≈ maxActive / 该值）")]
    [SerializeField] private int digestSpreadFrames = 4;

    [Header("碎片运动与外观")]
    [Tooltip("爆炸冲量下限（值越大碎片飞得越远）")]
    [SerializeField] private float burstImpulseMin = 1.0f;

    [Tooltip("爆炸冲量上限")]
    [SerializeField] private float burstImpulseMax = 2.5f;

    [Tooltip("碎片旋转速度范围（度/秒），值越大转得越疯")]
    [SerializeField] private float torqueRangeDegPerSec = 600f;

    [Tooltip("碎片存活时间（秒）")]
    [SerializeField] private float fragmentLifetime = 1.0f;

    [Tooltip("碎片字号倍率（1=跟原文字一样大）")]
    [SerializeField] private float fragmentFontSizeMul = 1f;

    [Header("Boss 死亡强化（LastWaveBossMarker 敌人专属）")]
    [Tooltip("Boss 每只怪最多入队字数倍率")]
    [SerializeField] private float bossFragmentCountMul = 4f;

    [Tooltip("Boss 碎片存活时间倍率")]
    [SerializeField] private float bossLifetimeMul = 2.5f;

    [Tooltip("Boss 碎片冲量倍率")]
    [SerializeField] private float bossBurstMul = 1.8f;

    [Tooltip("Boss 碎片字号额外放大倍率")]
    [SerializeField] private float bossFontSizeMul = 1.5f;

    [Header("普通碎片运动与外观")]
    [Tooltip("线性阻力（越小飞得越远越有炸裂感）")]
    [SerializeField] private float fragmentDrag = 0.8f;

    [Tooltip("角阻力（越小转得越久）")]
    [SerializeField] private float fragmentAngularDrag = 0.6f;

    [Tooltip("待生成碎片队列过长时丢弃最旧条目，防极端内存")]
    [SerializeField] private int maxPendingFragments = 2048;

    private readonly Queue<PendFragment> _pending = new Queue<PendFragment>(256);

    /// <summary>活跃碎片（集中 Tick，无物理/独立 Update）</summary>
    private readonly List<ActiveFragment> _activeFragments = new List<ActiveFragment>(64);

    private struct ActiveFragment
    {
        public GameObject go;
        public Transform tr;
        public Vector2 velocity;
        public float angularVelocity;
        public float releaseAt;
    }

    /// <summary>全局活跃碎片（跨实例，避免 Runtime 先于碎片销毁时计数泄漏）</summary>
    private static int s_globalActiveFragments;

    private int MaxNewFragmentsPerFrame =>
        Mathf.Max(1, maxActiveFragments / Mathf.Max(1, digestSpreadFrames));

    private struct PendFragment
    {
        public Vector3 worldPos;
        public Quaternion worldRot;
        public char character;
        public TMP_FontAsset font;
        public Material fontSharedMaterial;
        public float fontSize;
        public Color color;
        public bool enableVertexGradient;
        public VertexGradient colorGradient;
        public float outlineWidth;
        public Color outlineColor;
        public int sortingOrder;
        public bool isBoss;
        public float lifeMul;
        public float burstMul;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WordMonsterShatterRuntime] 重复实例，销毁多余的。");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 防御：场景重载时强制重置静态计数器（避免上一局 OnDestroy 未触发导致的泄漏）
        s_globalActiveFragments = 0;
        _spawnFailCount = 0;
        _spawnSuccessCount = 0;
    }

    private void OnDestroy()
    {
        // 清理所有活跃碎片 + 待生成队列，重置静态计数器（防止跨场景泄漏导致偶现失效）
        for (int i = _activeFragments.Count - 1; i >= 0; i--)
        {
            var f = _activeFragments[i];
            if (f.go != null) Destroy(f.go);
        }
        _activeFragments.Clear();
        _pending.Clear();
        s_globalActiveFragments = 0;
        _spawnFailCount = 0;
        _spawnSuccessCount = 0;

        if (Instance == this)
            Instance = null;
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
    }

    private void OnEnable()
    {
        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
    }

    private void Update()
    {
        int capThisFrame = Mathf.Min(MaxNewFragmentsPerFrame, maxActiveFragments - s_globalActiveFragments);
        int spawned = 0;
        while (spawned < capThisFrame && _pending.Count > 0)
        {
            PendFragment p = _pending.Dequeue();
            SpawnOne(p);
            spawned++;
            _spawnSuccessCount++;
        }

        TickFragments(Time.deltaTime);

        // 每 3 秒汇总
        if (Time.frameCount % 180 == 0 && (_pending.Count > 0 || _activeFragments.Count > 0))
            GameLog.Info($"[Shatter] Status: active={_activeFragments.Count}/{maxActiveFragments} pending={_pending.Count} success={_spawnSuccessCount} fail={_spawnFailCount}");
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (e.enemy == null)
        {
            GameLog.Info("[Shatter] OnEnemyDied: e.enemy is null, skip");
            return;
        }
        bool isBoss = e.enemy.GetComponent<LastWaveBossMarker>() != null;

        var label = e.enemy.GetComponentInChildren<EnemyWordLabel>(true);
        if (label == null)
        {
            GameLog.Info($"[Shatter] OnEnemyDied: no EnemyWordLabel on '{e.enemy.name}' (type={e.enemy.GetType().Name}), skip");
            return;
        }
        if (!label.TryGetWorldTextForShatter(out TextMeshPro src))
        {
            GameLog.Info($"[Shatter] OnEnemyDied: TryGetWorldTextForShatter false on '{e.enemy.name}', skip");
            return;
        }

        src.ForceMeshUpdate(true);
        TMP_TextInfo textInfo = src.textInfo;
        if (textInfo == null || textInfo.characterCount == 0)
        {
            GameLog.Info($"[Shatter] OnEnemyDied: textInfo null or charCount=0 on '{e.enemy.name}', skip");
            return;
        }

        // 正常路径日志已屏蔽，避免刷屏（line 195）

        int fragmentCap = isBoss
            ? Mathf.Max(maxFragmentsPerEnemy, Mathf.RoundToInt(maxFragmentsPerEnemy * bossFragmentCountMul))
            : maxFragmentsPerEnemy;
        float fontSizeMul = fragmentFontSizeMul * (isBoss ? bossFontSizeMul : 1f);

        Transform srcTr = src.transform;
        int added = 0;
        for (int i = 0; i < textInfo.characterCount && added < fragmentCap; i++)
        {
            TMP_CharacterInfo ch = textInfo.characterInfo[i];
            if (!ch.isVisible)
                continue;
            if (char.IsWhiteSpace(ch.character))
                continue;

            Vector3 bl = srcTr.TransformPoint(ch.bottomLeft);
            Vector3 tr = srcTr.TransformPoint(ch.topRight);
            Vector3 center = (bl + tr) * 0.5f;

            Enqueue(new PendFragment
            {
                worldPos = center,
                worldRot = srcTr.rotation,
                character = ch.character,
                font = src.font,
                fontSharedMaterial = src.fontSharedMaterial,
                fontSize = src.fontSize * fontSizeMul,
                color = src.color,
                enableVertexGradient = src.enableVertexGradient,
                colorGradient = src.colorGradient,
                outlineWidth = src.outlineWidth,
                outlineColor = src.outlineColor,
                sortingOrder = src.sortingOrder + 2,
                isBoss = isBoss,
                lifeMul = isBoss ? bossLifetimeMul : 1f,
                burstMul = isBoss ? bossBurstMul : 1f,
            });
            added++;
        }
    }

    private void Enqueue(PendFragment p)
    {
        while (_pending.Count >= maxPendingFragments)
            _pending.Dequeue();
        _pending.Enqueue(p);
    }

    private static int _spawnFailCount;
    private static int _spawnSuccessCount;

    private void SpawnOne(PendFragment p)
    {
        if (p.font == null || p.fontSharedMaterial == null)
        {
            if (_spawnFailCount++ < 5)
                Debug.LogWarning($"[Shatter] SpawnOne: font or material null for char='{p.character}' font={p.font?.name ?? "null"} mat={p.fontSharedMaterial?.name ?? "null"}");
            return;
        }

        var go = new GameObject($"ShatterChar_{p.character}");
        go.transform.SetPositionAndRotation(p.worldPos, p.worldRot);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.font = p.font;
        tmp.fontSharedMaterial = p.fontSharedMaterial;
        tmp.fontSize = p.fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = p.character.ToString();
        tmp.color = p.color;
        tmp.enableVertexGradient = p.enableVertexGradient;
        if (p.enableVertexGradient)
            tmp.colorGradient = p.colorGradient;
        tmp.outlineWidth = p.outlineWidth;
        tmp.outlineColor = p.outlineColor;
        tmp.sortingOrder = p.sortingOrder;
        tmp.ForceMeshUpdate(true);

        // 纯 Transform 运动，无 Rigidbody2D / 无物理 / 无独立 Update
        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 1e-6f)
            dir = Vector2.right;
        dir.Normalize();
        float impMin = p.isBoss ? burstImpulseMin * p.burstMul : burstImpulseMin;
        float impMax = p.isBoss ? burstImpulseMax * p.burstMul : burstImpulseMax;
        float imp = Random.Range(impMin, impMax);

        float life = p.isBoss ? fragmentLifetime * p.lifeMul : fragmentLifetime;

        _activeFragments.Add(new ActiveFragment
        {
            go = go,
            tr = go.transform,
            velocity = dir * imp,
            angularVelocity = Random.Range(-torqueRangeDegPerSec, torqueRangeDegPerSec),
            releaseAt = Time.time + Mathf.Max(0.05f, life),
        });
        s_globalActiveFragments++;
    }

    /// <summary>集中 Tick 所有碎片（无 Rigidbody2D、无独立 MonoBehaviour.Update）。</summary>
    private void TickFragments(float dt)
    {
        float dragMul = 1f - fragmentDrag * dt;
        float angularDragMul = 1f - fragmentAngularDrag * dt;

        for (int i = _activeFragments.Count - 1; i >= 0; i--)
        {
            var f = _activeFragments[i];
            if (Time.time >= f.releaseAt || f.go == null)
            {
                ReleaseFragmentAt(i);
                continue;
            }

            f.velocity *= dragMul;
            f.angularVelocity *= angularDragMul;
            f.tr.position += (Vector3)(f.velocity * dt);
            f.tr.Rotate(0f, 0f, f.angularVelocity * dt);
            _activeFragments[i] = f;
        }
    }

    private void ReleaseFragmentAt(int index)
    {
        int last = _activeFragments.Count - 1;
        var f = _activeFragments[index];
        if (f.go != null) Destroy(f.go);
        if (index < last) _activeFragments[index] = _activeFragments[last];
        _activeFragments.RemoveAt(last);
        s_globalActiveFragments = Mathf.Max(0, s_globalActiveFragments - 1);
    }

    internal static void NotifyFragmentEnd()
    {
        s_globalActiveFragments = Mathf.Max(0, s_globalActiveFragments - 1);
    }
}
