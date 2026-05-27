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
    }

    private void OnDestroy()
    {
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
        }
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (e.enemy == null)
            return;
        bool isBoss = e.enemy.GetComponent<LastWaveBossMarker>() != null;

        var label = e.enemy.GetComponentInChildren<EnemyWordLabel>(true);
        if (label == null || !label.TryGetWorldTextForShatter(out TextMeshPro src))
            return;

        src.ForceMeshUpdate(true);
        TMP_TextInfo textInfo = src.textInfo;
        if (textInfo == null || textInfo.characterCount == 0)
            return;

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

    private void SpawnOne(PendFragment p)
    {
        if (p.font == null || p.fontSharedMaterial == null)
            return;

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

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.drag = fragmentDrag;
        rb.angularDrag = fragmentAngularDrag;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 1e-6f)
            dir = Vector2.right;
        dir.Normalize();
        float impMin = p.isBoss ? burstImpulseMin * p.burstMul : burstImpulseMin;
        float impMax = p.isBoss ? burstImpulseMax * p.burstMul : burstImpulseMax;
        float imp = Random.Range(impMin, impMax);
        rb.AddForce(dir * imp, ForceMode2D.Impulse);
        rb.angularVelocity = Random.Range(-torqueRangeDegPerSec, torqueRangeDegPerSec);

        float life = p.isBoss ? fragmentLifetime * p.lifeMul : fragmentLifetime;
        var track = go.AddComponent<WordMonsterShatterFragmentTracker>();
        track.Init(life);
        s_globalActiveFragments++;
    }

    internal static void NotifyFragmentEnd()
    {
        s_globalActiveFragments = Mathf.Max(0, s_globalActiveFragments - 1);
    }
}

/// <summary>碎片销毁或寿命结束时递减全局活跃计数。</summary>
internal sealed class WordMonsterShatterFragmentTracker : MonoBehaviour
{
    private float _releaseAt;

    public void Init(float lifetime)
    {
        _releaseAt = Time.time + Mathf.Max(0.05f, lifetime);
    }

    private void Update()
    {
        if (Time.time >= _releaseAt)
            Release();
    }

    private void OnDestroy()
    {
        Release();
    }

    private bool _released;

    private void Release()
    {
        if (_released)
            return;
        _released = true;
        WordMonsterShatterRuntime.NotifyFragmentEnd();
        Destroy(gameObject);
    }
}
