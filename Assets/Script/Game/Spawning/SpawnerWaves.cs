using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using ProtoTable;

/// <summary>
/// SpawnerWaves
/// - 常规刷怪：按 interval 周期性生成（可关）
/// - 爆兵：分波次生成（你选的 9.2.C），四周均匀分布在玩家周围的“生成环”上（方案 R）
/// - 规则：离玩家至少 Rmin；生成半径 Rspawn（固定环半径）
///
/// 说明：
/// - 这里先做“可配置数据结构”，后续你要“读取表格”时，可以把 WaveConfig 替换为 ScriptableObject/CSV/JSON 读取后赋值即可。
/// </summary>
[DisallowMultipleComponent]
public class SpawnerWaves : MonoBehaviour
{
    [System.Serializable]
    public class WaveConfig
    {
        [Tooltip("波次名称（仅用于调试/显示）")]
        public string name = "Wave";

        [Tooltip("本波生成的怪物ID（从 EnemyCatalog 查配置）")]
        public int enemyId = 1;

        [Tooltip("本波生成数量")]
        public int count = 20;

        [Tooltip("本波内每只怪的生成间隔（秒）。例如 0 表示一口气生成完。")]
        public float spawnStep = 0.02f;
    }

    [Header("Prefab")]
    [Tooltip("敌人预制体（兼容旧用法）。如果你改为按 enemyId 生成，可以不填。")]
    public GameObject enemyPrefab;

    [Header("按ID生成（推荐）")]
    [Tooltip("怪物目录表（Inspector里配置ID->Prefab/数值/资源引用）。按ID生成时需要。")]
    public EnemyCatalog catalog;

    [Tooltip("常规刷怪使用的怪物ID（当启用常规刷怪时使用）")]
    public int normalEnemyId = 1;

    [Header("目标（玩家）")]
    [Tooltip("刷怪围绕的目标。若为空，会尝试用 Tag=Player 查找。")]
    public Transform target;

    [Tooltip("当 target 为空时，用该 Tag 自动查找。")]
    public string targetTag = "Player";

    [Header("常规刷怪（可选）")]
    [Tooltip("是否启用常规刷怪（按 interval 持续刷）")]
    public bool enableNormalSpawn = true;

    [Tooltip("常规刷怪间隔（秒）")]
    public float normalInterval = 1f;

    [Tooltip("常规刷怪每次生成数量")]
    public int normalCountPerTick = 1;

    [Header("生成环参数（方案R）")]
    [Tooltip("最小安全距离：怪物生成点到玩家至少该距离（世界单位）。")]
    public float rMin = 5f;

    [Tooltip("生成环半径：怪物会在距离玩家约该半径的位置生成（世界单位）。")]
    public float rSpawn = 10f;

    [Tooltip("位置扰动（世界单位）：让生成点在环上有少许随机偏移，避免完全规则。")]
    public float ringJitter = 0.3f;

    [Header("地图边界限制（必须在地图内生成）")]
    [Tooltip("用于判断“地图内/外”的 Tilemap（建议拖 GroundTilemap）。为空则不做地图内限制。")]
    public Tilemap mapBoundsTilemap;

    [Tooltip("生成点距离地图边界的最小留边（世界单位）。例如 0.5 表示不要刷在边界墙正贴边的位置。")]
    public float mapPadding = 0.5f;

    [Tooltip("每次生成时最多尝试多少次找一个在地图内的点（玩家靠边时很重要）。")]
    public int maxTries = 12;

    [Header("敌人参数（可选）")]
    [Tooltip("是否写入敌人移动速度（EnemyAI.moveSpeed）")]
    public bool applyMoveSpeed = true;

    [Tooltip("敌人速度范围（随机）")]
    public Vector2 enemySpeedRange = new Vector2(1.5f, 3.5f);

    [Tooltip("是否写入敌人血量（EnemyStats.hp/maxHp）。若敌人没有该组件会自动挂上。")]
    public bool applyHp = true;

    [Tooltip("敌人血量范围（随机）")]
    public Vector2 enemyHpRange = new Vector2(3f, 10f);

    [Header("爆兵（分波次）")]
    [Tooltip("波次数组（按顺序执行）")]
    public WaveConfig[] waves;

    [Tooltip("波与波之间的间隔（秒）")]
    public float waveInterval = 1.0f;

    [Tooltip("是否在游戏开始后自动触发爆兵（用于测试）。正式触发可由倒计时/击杀数调用 TriggerWaves。")]
    public bool autoTriggerWavesOnStart = true;

    [Tooltip("开始后延迟多久触发爆兵（秒）")]
    public float autoTriggerDelay = 10f;

    [Tooltip("是否使用真实时间计时（不受 Time.timeScale 影响）。建议开启，避免暂停时爆兵不触发。")]
    public bool useRealtimeForWaves = true;

    [Tooltip("触发爆兵时，是否暂停常规刷怪；爆兵结束后恢复。")]
    public bool pauseNormalDuringWaves = true;

    [Tooltip("是否在 Console 打印波次触发/生成统计（用于确认爆兵是否生效）。")]
    public bool debugLogs = true;

    [Header("LevelWave 表驱动（可选）")]
    [Tooltip("为 true 时优先从 TableManager 的 LevelWave 表按关卡取波次；无数据则回退到上方 waves 数组。")]
    public bool useLevelWaveTable = false;

    [Tooltip("关卡 ID，与 LevelWave.levelId 一致。0 = 自动：RoguelikeCardManager.CurrentLevel，再尝试 CardSelectionSystem.currentLevel，最后为 1。")]
    public int levelWaveLevelId = 0;

    [Header("文字怪整波底色")]
    [Tooltip("每波开始时刷新一次「整波统一」面色；EnemyWordLabel 上 preferWaveSharedTint 为真时才会用")]
    [SerializeField] private WordMonsterWaveTintMode wordMonsterWaveTintMode = WordMonsterWaveTintMode.RandomPerWave;

    private Coroutine _normalRoutine;
    private Coroutine _waveRoutine;
    private Coroutine _autoRoutine;

    private struct TableWaveRuntime
    {
        public int wave;
        public int monsterId;
        public int totalMonster;
        public float intervalSpawn;
        public int timeStart;
        public int waveTimeContinue;
        public int lineSpawn;
        public int attack;
        public int maxHp;

        public static TableWaveRuntime From(LevelWave lw)
        {
            return new TableWaveRuntime
            {
                wave = lw.wave,
                monsterId = lw.monsterId,
                totalMonster = lw.totalMonster,
                intervalSpawn = lw.intervalSpawn,
                timeStart = lw.timeStart,
                waveTimeContinue = lw.waveTimeContinue,
                lineSpawn = lw.lineSpawn,
                attack = lw.attack,
                maxHp = lw.maxHp,
            };
        }
    }

    private void Awake()
    {
        // 强制生命周期日志：用于确认脚本是否真的在运行（不受 debugLogs 开关影响）
        Debug.Log($"[SpawnerWaves] Awake name={name} activeInHierarchy={gameObject.activeInHierarchy} enabled={enabled}");
    }

    private void OnEnable()
    {
        Debug.Log($"[SpawnerWaves] OnEnable name={name} autoTriggerWavesOnStart={autoTriggerWavesOnStart} delay={autoTriggerDelay} enableNormalSpawn={enableNormalSpawn}");

        if (_normalRoutine == null)
            _normalRoutine = StartCoroutine(NormalLoop());

        if (autoTriggerWavesOnStart && _autoRoutine == null)
            _autoRoutine = StartCoroutine(TriggerWavesAfterDelay());
    }

    private void Start()
    {
        // 再补一层保险：有些情况下脚本启用顺序/运行时实例替换会导致你以为勾了但没触发
        if (autoTriggerWavesOnStart && _autoRoutine == null)
        {
            Debug.Log("[SpawnerWaves] Start: auto trigger scheduled.");
            _autoRoutine = StartCoroutine(TriggerWavesAfterDelay());
        }
    }

    private void OnDisable()
    {
        Debug.Log($"[SpawnerWaves] OnDisable name={name} (coroutines will stop)");
        if (_normalRoutine != null) { StopCoroutine(_normalRoutine); _normalRoutine = null; }
        if (_waveRoutine != null) { StopCoroutine(_waveRoutine); _waveRoutine = null; }
        if (_autoRoutine != null) { StopCoroutine(_autoRoutine); _autoRoutine = null; }
    }

    private IEnumerator NormalLoop()
    {
        while (true)
        {
            if (enableNormalSpawn)
            {
                for (int i = 0; i < Mathf.Max(1, normalCountPerTick); i++)
                    SpawnOne(normalEnemyId);
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, normalInterval));
        }
    }

    private IEnumerator TriggerWavesAfterDelay()
    {
        float d = Mathf.Max(0f, autoTriggerDelay);
        Debug.Log($"[SpawnerWaves] Waiting {d}s before auto-trigger (realtime={useRealtimeForWaves}, timeScale={Time.timeScale}).");
        if (useRealtimeForWaves) yield return new WaitForSecondsRealtime(d);
        else yield return new WaitForSeconds(d);
        Debug.Log($"[SpawnerWaves] Auto-trigger NOW name={name} activeInHierarchy={gameObject.activeInHierarchy} enabled={enabled}");
        _autoRoutine = null;
        TriggerWaves();
    }

    /// <summary>
    /// 对外 API：触发爆兵（分波次）。
    /// 你后期从表格读取波次数据后，也可以在调用前把 waves 数组替换掉。
    /// </summary>
    public void TriggerWaves()
    {
        if (_waveRoutine != null) return; // 避免重复触发叠加
        Debug.Log("[SpawnerWaves] TriggerWaves()");
        _waveRoutine = StartCoroutine(WaveRoutine());
    }

    private IEnumerator WaveRoutine()
    {
        List<TableWaveRuntime> tableWaves = null;
        int resolvedLevelId = ResolveLevelWaveLevelId();
        if (useLevelWaveTable)
        {
            tableWaves = BuildTableWavesForLevel(resolvedLevelId);
            if (debugLogs)
                Debug.Log($"[SpawnerWaves] LevelWave 表 levelId={resolvedLevelId} 命中行数={tableWaves.Count}");
        }

        bool useTable = tableWaves != null && tableWaves.Count > 0;

        if (!useTable && (waves == null || waves.Length == 0))
        {
            Debug.LogWarning("SpawnerWaves: 未启用表数据或表为空，且 waves 数组为空。");
            _waveRoutine = null;
            yield break;
        }

        bool prevNormal = enableNormalSpawn;
        if (pauseNormalDuringWaves) enableNormalSpawn = false;

        WordMonsterWaveStyle.ClearWaveTint();

        if (useTable)
        {
            for (int w = 0; w < tableWaves.Count; w++)
            {
                TableWaveRuntime tw = tableWaves[w];
                if (debugLogs)
                    Debug.Log($"[SpawnerWaves] LevelWave 波次 {w + 1}/{tableWaves.Count} wave#{tw.wave} monsterId={tw.monsterId} cap={tw.totalMonster}");

                WordMonsterWaveStyle.ApplyWaveStart(wordMonsterWaveTintMode, resolvedLevelId, tw.wave);
                yield return TableWaveSpawnRoutine(tw, w + 1, tableWaves.Count);

                if (w < tableWaves.Count - 1)
                {
                    float wi = Mathf.Max(0f, waveInterval);
                    if (useRealtimeForWaves) yield return new WaitForSecondsRealtime(wi);
                    else yield return new WaitForSeconds(wi);
                }
            }
        }
        else
        {
            for (int w = 0; w < waves.Length; w++)
            {
                WaveConfig cfg = waves[w];
                int count = Mathf.Max(0, cfg.count);
                float step = Mathf.Max(0f, cfg.spawnStep);

                if (debugLogs) Debug.Log($"[SpawnerWaves] Wave {w + 1}/{waves.Length} '{cfg.name}' count={count} step={step}");

                WordMonsterWaveStyle.ApplyWaveStart(wordMonsterWaveTintMode, resolvedLevelId, w + 1);
                int spawned = 0;
                for (int i = 0; i < count; i++)
                {
                    if (SpawnLimiter.Instance != null)
                    {
                        if (!SpawnLimiter.Instance.CanSpawn("Enemy", out var limitCfg))
                        {
                            if (limitCfg != null && limitCfg.spawnPerFrame > 0)
                            {
                                yield return null;
                                i--;
                                continue;
                            }
                            break;
                        }
                    }

                    SpawnOne(cfg.enemyId, 0, 0, 0);
                    spawned++;

                    if (step > 0f)
                    {
                        if (useRealtimeForWaves) yield return new WaitForSecondsRealtime(step);
                        else yield return new WaitForSeconds(step);
                    }
                }

                if (debugLogs) Debug.Log($"[SpawnerWaves] Wave {w + 1} 完成，实际生成 {spawned}/{count}");

                if (w < waves.Length - 1)
                {
                    float wi = Mathf.Max(0f, waveInterval);
                    if (useRealtimeForWaves) yield return new WaitForSecondsRealtime(wi);
                    else yield return new WaitForSeconds(wi);
                }
            }
        }

        if (pauseNormalDuringWaves) enableNormalSpawn = prevNormal;
        WordMonsterWaveStyle.ClearWaveTint();
        _waveRoutine = null;
    }

    /// <summary>
    /// 模型 II：先 timeStart，再在整个 waveTimeContinue 内按 interval 刷，最多 totalMonster；时间到即停（1.A）。
    /// </summary>
    private IEnumerator TableWaveSpawnRoutine(TableWaveRuntime tw, int displayIndex, int displayTotal)
    {
        int pre = Mathf.Max(0, tw.timeStart);
        if (pre > 0)
        {
            if (debugLogs)
                Debug.Log($"[SpawnerWaves] 波 {displayIndex}/{displayTotal} 开场等待 timeStart={pre}s");
            if (useRealtimeForWaves) yield return new WaitForSecondsRealtime(pre);
            else yield return new WaitForSeconds(pre);
        }

        int cap = Mathf.Max(0, tw.totalMonster);
        float step = Mathf.Max(0f, tw.intervalSpawn);
        int windowSec = Mathf.Max(0, tw.waveTimeContinue);

        float startT = useRealtimeForWaves ? Time.realtimeSinceStartup : Time.time;
        int spawned = 0;

        while (spawned < cap)
        {
            if (windowSec > 0)
            {
                float now = useRealtimeForWaves ? Time.realtimeSinceStartup : Time.time;
                if (now - startT >= windowSec)
                    break;
            }

            if (SpawnLimiter.Instance != null)
            {
                if (!SpawnLimiter.Instance.CanSpawn("Enemy", out var limitCfg))
                {
                    if (limitCfg != null && limitCfg.spawnPerFrame > 0)
                    {
                        yield return null;
                        continue;
                    }
                    break;
                }
            }

            SpawnOne(tw.monsterId, tw.lineSpawn, tw.attack, tw.maxHp);
            spawned++;

            if (spawned >= cap)
                break;

            if (step > 0f)
            {
                if (useRealtimeForWaves) yield return new WaitForSecondsRealtime(step);
                else yield return new WaitForSeconds(step);
            }
            else
                yield return null;
        }

        if (debugLogs)
            Debug.Log($"[SpawnerWaves] 波 {displayIndex}/{displayTotal}（表 wave={tw.wave}）结束：已刷 {spawned}/{cap}，窗长 {windowSec}s（0=不限）");
    }

    private List<TableWaveRuntime> BuildTableWavesForLevel(int levelId)
    {
        var result = new List<TableWaveRuntime>();
        if (TableManager.Instance == null)
            return result;

        var dict = TableManager.Instance.GetTable<LevelWave>();
        if (dict == null || dict.Count == 0)
            return result;

        foreach (var kv in dict)
        {
            if (kv.Value is LevelWave lw && lw.levelId == levelId)
                result.Add(TableWaveRuntime.From(lw));
        }

        result.Sort((a, b) => a.wave.CompareTo(b.wave));

        return result;
    }

    private int ResolveLevelWaveLevelId()
    {
        if (levelWaveLevelId > 0)
            return levelWaveLevelId;

        if (RoguelikeCardManager.Instance != null)
            return RoguelikeCardManager.Instance.CurrentLevel;

        CardSelectionSystem css = FindObjectOfType<CardSelectionSystem>();
        if (css != null)
            return css.currentLevel;

        return 1;
    }

    private void SpawnOne(int enemyId)
    {
        SpawnOne(enemyId, 0, 0, 0);
    }

    private void SpawnOne(int enemyId, int lineSpawn, int attackOverride, int maxHpOverride)
    {
        if (SpawnLimiter.Instance != null)
        {
            if (!SpawnLimiter.Instance.CanSpawn("Enemy", out var cfg))
            {
                if (debugLogs && cfg != null && !cfg.recycleOldest)
                    Debug.Log($"[SpawnerWaves] 达到怪物上限，暂不生成 enemyId={enemyId}");
                return;
            }
        }

        if (target == null) TryFindTarget();
        if (target == null) return;

        Vector2 center = target.position;
        int ls = NormalizeLineSpawn(lineSpawn);
        if (!TryGetSpawnPos(ls, center, out Vector2 pos))
            return;

        GameObject prefabToSpawn = ResolvePrefab(enemyId, out EnemyDefinition def);
        if (prefabToSpawn == null)
        {
            if (debugLogs) Debug.LogWarning($"[SpawnerWaves] 找不到 enemyId={enemyId} 的配置或prefab。");
            return;
        }

        GameObject enemy = GameObjectPool.Get(prefabToSpawn, pos, Quaternion.identity);
        SpawnLimiter.Instance?.RegisterSpawned("Enemy", enemy);

        EnemyBase eb = enemy.GetComponent<EnemyBase>();
        if (eb != null && def != null)
        {
            eb.InitFromDefinition(def);
            if (attackOverride > 0 || maxHpOverride > 0)
                eb.ApplyWaveStatOverrides(attackOverride, maxHpOverride);
            MonsterWordSpawnBinding.TryApply(enemy, enemyId);
            return;
        }

        if (applyMoveSpeed)
        {
            float spd = Random.Range(enemySpeedRange.x, enemySpeedRange.y);
            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null) ai.moveSpeed = spd;
        }

        if (applyHp || maxHpOverride > 0 || attackOverride > 0)
        {
            EnemyStats stats = enemy.GetComponent<EnemyStats>();
            if (stats == null) stats = enemy.AddComponent<EnemyStats>();
            if (maxHpOverride > 0)
            {
                stats.maxHp = maxHpOverride;
                stats.hp = maxHpOverride;
            }
            else if (applyHp)
            {
                float hp = Random.Range(enemyHpRange.x, enemyHpRange.y);
                stats.hp = hp;
                stats.maxHp = Mathf.Max(stats.maxHp, hp);
            }
        }

        MonsterWordSpawnBinding.TryApply(enemy, enemyId);
    }

    private GameObject ResolvePrefab(int enemyId, out EnemyDefinition def)
    {
        def = null;

        if (catalog != null && catalog.TryGet(enemyId, out def))
        {
            if (def != null && def.prefab != null) return def.prefab;
        }

        // 兜底：兼容旧字段 enemyPrefab
        return enemyPrefab;
    }

    private static int NormalizeLineSpawn(int lineSpawn)
    {
        if (lineSpawn >= 0 && lineSpawn <= 4)
            return lineSpawn;
        return 0;
    }

    private bool TryGetSpawnPos(int lineSpawn, Vector2 center, out Vector2 pos)
    {
        if (lineSpawn == 0)
            return TryGetRingSpawnPosInsideMap(center, out pos);
        return TryGetEdgeSpawnPosInsideMap(center, lineSpawn, out pos);
    }

    /// <summary>
    /// lineSpawn：1 上、2 下、3 左、4 右，在玩家外侧约 rSpawn 处取点并约束在地图内。
    /// </summary>
    private bool TryGetEdgeSpawnPosInsideMap(Vector2 center, int lineSpawn, out Vector2 pos)
    {
        Vector2 dir = lineSpawn == 1 ? Vector2.up
            : lineSpawn == 2 ? Vector2.down
            : lineSpawn == 3 ? Vector2.left
            : Vector2.right;

        float baseDist = Mathf.Max(rMin, rSpawn);
        int tries = Mathf.Max(1, maxTries);

        for (int i = 0; i < tries; i++)
        {
            float dist = baseDist + Random.Range(-ringJitter, ringJitter);
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float side = Random.Range(-ringJitter, ringJitter) * 2f;
            Vector2 candidate = center + dir * dist + perp * side;
            if (IsInsideMap(candidate))
            {
                pos = candidate;
                return true;
            }
        }

        if (mapBoundsTilemap != null)
        {
            pos = ClampToMap(center + dir * baseDist);
            return true;
        }

        pos = center + dir * baseDist;
        return true;
    }

    private Vector2 GetRingSpawnPos(Vector2 center)
    {
        float radius = Mathf.Max(rSpawn, rMin + 0.01f);
        float angle = Random.value * Mathf.PI * 2f; // 四周均匀一圈
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        float jitter = Random.Range(-ringJitter, ringJitter);
        float r = Mathf.Max(rMin, radius + jitter);

        return center + dir * r;
    }

    private bool TryGetRingSpawnPosInsideMap(Vector2 center, out Vector2 pos)
    {
        int tries = Mathf.Max(1, maxTries);
        for (int i = 0; i < tries; i++)
        {
            Vector2 candidate = GetRingSpawnPos(center);
            if (IsInsideMap(candidate))
            {
                pos = candidate;
                return true;
            }
        }

        if (mapBoundsTilemap != null)
        {
            // 兜底：夹紧到地图内，保证不会刷到地图外
            pos = ClampToMap(center);
            return true;
        }

        pos = default;
        return false;
    }

    private bool IsInsideMap(Vector2 worldPos)
    {
        if (mapBoundsTilemap == null) return true;

        BoundsInt cellBounds = mapBoundsTilemap.cellBounds;
        Vector3Int cell = mapBoundsTilemap.WorldToCell(worldPos);
        if (!cellBounds.Contains(cell)) return false;

        // 用 Tilemap 的世界 bounds 做留边判断（矩形留边，足以解决“刷到地图外”）
        Bounds local = mapBoundsTilemap.localBounds;
        Vector3 c = mapBoundsTilemap.transform.TransformPoint(local.center);
        Vector3 ext = Vector3.Scale(local.extents, mapBoundsTilemap.transform.lossyScale);
        Bounds world = new Bounds(c, ext * 2f);

        float pad = Mathf.Max(0f, mapPadding);
        return worldPos.x >= world.min.x + pad && worldPos.x <= world.max.x - pad &&
               worldPos.y >= world.min.y + pad && worldPos.y <= world.max.y - pad;
    }

    private Vector2 ClampToMap(Vector2 preferred)
    {
        Bounds local = mapBoundsTilemap.localBounds;
        Vector3 c = mapBoundsTilemap.transform.TransformPoint(local.center);
        Vector3 ext = Vector3.Scale(local.extents, mapBoundsTilemap.transform.lossyScale);
        Bounds world = new Bounds(c, ext * 2f);

        float pad = Mathf.Max(0f, mapPadding);
        float x = Mathf.Clamp(preferred.x, world.min.x + pad, world.max.x - pad);
        float y = Mathf.Clamp(preferred.y, world.min.y + pad, world.max.y - pad);
        return new Vector2(x, y);
    }

    private void TryFindTarget()
    {
        if (string.IsNullOrWhiteSpace(targetTag)) return;
        GameObject go = GameObject.FindGameObjectWithTag(targetTag);
        if (go != null) target = go.transform;
    }
}

