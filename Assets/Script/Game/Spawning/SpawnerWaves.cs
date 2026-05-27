using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using ProtoTable;
using UnityEngine.SceneManagement;
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
    [Tooltip("敌人预制体（按 ID 在 EnemyCatalog 找不到时兜底使用）。")]
    public GameObject enemyPrefab;

    [Header("按ID生成（推荐）")]
    [Tooltip("怪物目录表（Inspector里配置ID->Prefab映射）。数值不再从此读取，统一走 Excel 表。")]
    public EnemyCatalog catalog;

    [Tooltip("常规刷怪使用的怪物ID（当启用常规刷怪时使用）")]
    public int normalEnemyId = 1;

    [Header("目标（玩家）")]
    public Transform target;
    public string targetTag = "Player";

    [Header("常规刷怪（可选）")]
    public bool enableNormalSpawn = true;
    public float normalInterval = 1f;
    public int normalCountPerTick = 1;

    [Header("生成环参数（方案R）")]
    public float rMin = 5f;
    public float rSpawn = 10f;
    public float ringJitter = 0.3f;

    [Header("地图边界限制")]
    public Tilemap mapBoundsTilemap;
    public float mapPadding = 0.5f;
    public int maxTries = 12;

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

    [Header("屏幕警告闪红")]
    [Tooltip("波次怪物数量超过此值时触发红色警告。")]
    [SerializeField] private int waveWarningThreshold = 100;

    [Tooltip("普通波次警告颜色（大批量怪物）。")]
    [SerializeField] private Color waveWarningColor = new Color(1f, 0.08f, 0.08f, 0.35f);

    [Tooltip("Boss 波次警告颜色。")]
    [SerializeField] private Color bossWarningColor = new Color(0.9f, 0.2f, 0f, 0.45f);

    [Tooltip("警告脉冲次数。")]
    [SerializeField] private int warningPulseCount = 2;

    [Tooltip("警告总时长（秒）。")]
    [SerializeField] private float warningDuration = 0.6f;

    [Header("文字怪整波底色")]
    [Tooltip("每波开始时刷新一次「整波统一」面色；EnemyWordLabel 上 preferWaveSharedTint 为真时才会用")]
    [SerializeField] private WordMonsterWaveTintMode wordMonsterWaveTintMode = WordMonsterWaveTintMode.RandomPerWave;

    private Coroutine _normalRoutine;
    private Coroutine _waveRoutine;
    private Coroutine _autoRoutine;

    /// <summary>
    /// 本实例是否已走完一次爆兵协程并发出 <see cref="BattleWavesCompletedEvent"/>（供 <see cref="BattleOutcomeCoordinator"/> 轮询兜底）。
    /// </summary>
    public bool HasReleasedWaveCompletionSignal { get; private set; }

    /// <summary>当前关卡配置的爆兵总波数（表驱动或 waves 数组）。</summary>
    public int GetConfiguredWaveCount()
    {
        if (useLevelWaveTable)
        {
            int levelId = ResolveLevelWaveLevelId();
            int n = LevelWaveCatalog.CountWavesForLevel(levelId);
            if (n > 0)
                return n;
        }

        return waves != null ? waves.Length : 0;
    }

    /// <summary>表驱动时本关有 LevelWave 行，或未开表时 Inspector waves 非空。</summary>
    public bool HasValidLevelWaveConfiguration()
    {
        if (useLevelWaveTable)
            return LevelWaveCatalog.HasWavesForLevel(ResolveLevelWaveLevelId());

        return waves != null && waves.Length > 0;
    }

    private static void PublishWaveChanged(SpawnerWaves spawner, int currentWave, int totalWaves)
    {
        if (totalWaves <= 0)
            return;

        EventBus.Publish(new BattleWaveChangedEvent
        {
            currentWave = currentWave,
            totalWaves = totalWaves,
            spawner = spawner,
        });
    }

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
        public float moveSpeed; // LevelWave.speed，直接使用
        public bool isBoss;
        public int quantityBoss;

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
                moveSpeed = lw.speed,
                isBoss = lw.isBoss,
                quantityBoss = lw.quantityBoss,
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
        HasReleasedWaveCompletionSignal = false;

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
            if (useLevelWaveTable){

                GameErrorPresenter.Show(GameErrorCodes.LevelWaveConfigMissing, () =>
                {
                    SceneManager.LoadScene("Home");
                },resolvedLevelId);
                Debug.LogWarning($"[SpawnerWaves] 关卡 {resolvedLevelId} 缺少 LevelWave 波次配置，不触发波次完成信号。");
            }
            else{
                GameErrorPresenter.Show(GameErrorCodes.LevelWaveTableMissing);
                Debug.LogWarning("SpawnerWaves: 未启用表数据且 waves 数组为空。");
            }
            _waveRoutine = null;
            yield break;
        }

        bool prevNormal = enableNormalSpawn;
        if (pauseNormalDuringWaves) enableNormalSpawn = false;

        WordMonsterWaveStyle.ClearWaveTint();

        if (useTable)
        {
            int total = tableWaves.Count;
            for (int w = 0; w < total; w++)
            {
                TableWaveRuntime tw = tableWaves[w];
                if (debugLogs)
                    Debug.Log($"[SpawnerWaves] LevelWave 波次 {w + 1}/{total} wave#{tw.wave} monsterId={tw.monsterId} cap={tw.totalMonster}");

                PublishWaveChanged(this, w + 1, total);
                WordMonsterWaveStyle.ApplyWaveStart(wordMonsterWaveTintMode, resolvedLevelId, tw.wave);
                bool isLastWave = w == total - 1;
                yield return TableWaveSpawnRoutine(tw, w + 1, tableWaves.Count, isLastWave);

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
            int total = waves.Length;
            for (int w = 0; w < total; w++)
            {
                WaveConfig cfg = waves[w];
                int count = Mathf.Max(0, cfg.count);
                float step = Mathf.Max(0f, cfg.spawnStep);

                if (debugLogs) Debug.Log($"[SpawnerWaves] Wave {w + 1}/{total} '{cfg.name}' count={count} step={step}");

                PublishWaveChanged(this, w + 1, total);
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

                    SpawnEnemy(cfg.enemyId, 0, 0, 0, 0);
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

        HasReleasedWaveCompletionSignal = true;
        EventBus.Publish(new BattleWavesCompletedEvent { spawner = this });

        _waveRoutine = null;
    }

    /// <summary>
    /// 模型 II：先 timeStart，再在整个 waveTimeContinue 内按 interval 刷，最多 totalMonster；时间到即停（1.A）。
    /// </summary>
    private IEnumerator TableWaveSpawnRoutine(TableWaveRuntime tw, int displayIndex, int displayTotal, bool isLastWave)
    {
        int bossMarkBudget = 0;
        if (isLastWave && tw.isBoss)
            bossMarkBudget = tw.quantityBoss > 0 ? tw.quantityBoss : 1;

        // 波次警告闪红（大批量 / Boss）
        TryFlashWaveWarning(tw, displayIndex);

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

            bool markBoss = bossMarkBudget > 0;
            if (markBoss)
                bossMarkBudget--;

            GameObject spawnedGo = SpawnEnemy(tw.monsterId, tw.lineSpawn, tw.attack, tw.maxHp, tw.moveSpeed);
            if (markBoss && spawnedGo != null)
                TryMarkLastWaveBoss(spawnedGo);
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

    /// <summary>
    /// 怪物数量超过阈值或 Boss 波次时，触发屏幕闪红警告。
    /// </summary>
    private void TryFlashWaveWarning(TableWaveRuntime tw, int displayIndex)
    {
        var coordinator = BattleOutcomeCoordinator.Instance;
        if (coordinator == null) return;

        if (tw.isBoss)
        {
            if (debugLogs)
                Debug.Log($"[SpawnerWaves] Boss 波次警告 wave={tw.wave} displayIndex={displayIndex}");
            coordinator.FlashWarning(bossWarningColor, warningPulseCount, warningDuration);
        }
        else if (tw.totalMonster >= waveWarningThreshold)
        {
            if (debugLogs)
                Debug.Log($"[SpawnerWaves] 大批量波次警告 wave={tw.wave} totalMonster={tw.totalMonster} >= {waveWarningThreshold}");
            coordinator.FlashWarning(waveWarningColor, warningPulseCount, warningDuration);
        }
    }

    private List<TableWaveRuntime> BuildTableWavesForLevel(int levelId)
    {
        var result = new List<TableWaveRuntime>();
        if (TableManager.Instance == null)
        {
            Debug.Log("[SpawnerWaves] BuildTableWaves: TableManager.Instance is null");
            return result;
        }

        var dict = TableManager.Instance.GetTable<LevelWave>();
        if (dict == null || dict.Count == 0)
        {
            Debug.Log($"[SpawnerWaves] BuildTableWaves: dict empty. dictNull={dict == null} count={dict?.Count ?? 0}");
            return result;
        }

        Debug.Log($"[SpawnerWaves] BuildTableWaves: dict.Count={dict.Count}, targetLevelId={levelId}");
        foreach (var kv in dict)
        {
            if (kv.Value is LevelWave lw)
            {
                if (lw.levelId == levelId)
                {
                    result.Add(TableWaveRuntime.From(lw));
                }
                else if (result.Count == 0 && kv.Key < 100) // 仅在前几条且未命中时打印
                {
                    Debug.Log($"[SpawnerWaves]   miss: key={kv.Key}, lw.levelId={lw.levelId}, target={levelId}");
                }
            }
            else
            {
                Debug.Log($"[SpawnerWaves]   type mismatch: key={kv.Key}, type={kv.Value?.GetType().Name ?? "null"}");
            }
        }

        result.Sort((a, b) => a.wave.CompareTo(b.wave));
        Debug.Log($"[SpawnerWaves] BuildTableWaves: result.Count={result.Count}");

        return result;
    }

    private int ResolveLevelWaveLevelId()
    {
        BattleLevelContext.LogMissingSelectionOnce(nameof(SpawnerWaves));
        return BattleLevelContext.LevelId;
    }

    private static void TryMarkLastWaveBoss(GameObject enemy)
    {
        if (enemy == null) return;
        if (enemy.GetComponent<LastWaveBossMarker>() == null)
            enemy.AddComponent<LastWaveBossMarker>();
        BattleVictoryBossTracker.RegisterBossSpawned();
    }

    /// <summary>常规刷怪（无 Excel 数据 → 攻血速走旧 Inspector 兜底）。</summary>
    private void SpawnOne(int enemyId)
    {
        SpawnEnemy(enemyId, 0, 0, 0, 0);
    }

    /// <summary>
    /// 核心生成方法：所有攻血速从 Excel（LevelWave 表）来，不再读 EnemyDefinition 或 Inspector。
    /// </summary>
    private GameObject SpawnEnemy(int enemyId, int lineSpawn, int attack, int maxHp, float moveSpeed)
    {
        if (SpawnLimiter.Instance != null)
        {
            if (!SpawnLimiter.Instance.CanSpawn("Enemy", out var cfg))
            {
                if (debugLogs && cfg != null && !cfg.recycleOldest)
                    Debug.Log($"[SpawnerWaves] 达到怪物上限，暂不生成 enemyId={enemyId}");
                return null;
            }
        }

        if (target == null) TryFindTarget();
        if (target == null) return null;

        Vector2 center = target.position;
        int ls = NormalizeLineSpawn(lineSpawn);
        if (!TryGetSpawnPos(ls, center, out Vector2 pos))
            return null;

        GameObject prefabToSpawn = ResolvePrefab(enemyId, out EnemyDefinition def);
        if (prefabToSpawn == null)
        {
            if (debugLogs) Debug.LogWarning($"[SpawnerWaves] 找不到 enemyId={enemyId} 的配置或prefab。");
            return null;
        }

        GameObject enemy = GameObjectPool.Get(prefabToSpawn, pos, Quaternion.identity);
        SpawnLimiter.Instance?.RegisterSpawned("Enemy", enemy);

        EnemyBase eb = enemy.GetComponent<EnemyBase>();
        if (eb != null)
        {
            if (def != null)
                eb.InitFromDefinition(def);
            eb.ApplyTableStats(attack, maxHp, moveSpeed);
        }

        MonsterWordSpawnBinding.TryApply(enemy, enemyId);
        return enemy;
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

