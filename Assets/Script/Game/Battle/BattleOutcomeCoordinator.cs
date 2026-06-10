using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内胜负判定 + 复活流程 + 结算延迟（让死亡/击杀动画播完再弹面板）。
/// </summary>
[DefaultExecutionOrder(-40)]
public sealed class BattleOutcomeCoordinator : MonoBehaviour
{
    [Header("结算延迟")]
    [Tooltip("Boss 击杀后等待多久再弹出胜利面板。")]
    [SerializeField] private float victoryDelaySeconds = 3.0f;

    [Tooltip("Boss 击杀慢动作的目标 timeScale（越小越慢）。")]
    [SerializeField] private float victoryTimeScale = 0.08f;

    [Tooltip("玩家死亡后等待多久再弹出失败面板（闪红+黑屏过渡）。")]
    [SerializeField] private float defeatDelaySeconds = 1.5f;

    [Header("死亡过渡")]
    [Tooltip("闪红持续时间（秒）。")]
    [SerializeField] private float deathFlashDuration = 0.3f;

    [Tooltip("黑屏过渡后的目标透明度（0=全透明, 1=全黑）。")]
    [SerializeField] private float deathFadeTargetAlpha = 0.85f;

    private GameLayer _gameLayer;
    private PlayerHealth _playerHealth;
    private bool _battleEnded;
    private bool _reviveOfferUsed;
    private bool _reviveFlowActive;

    // Boss 死亡位置（供 VictorySequence 锁镜头用）
    private Vector3 _bossDeathPosition;
    private bool _hasBossDeathPosition;

    // 全屏遮罩（死亡渐黑/闪红 + 后续波次警告共用）
    private Image _screenOverlay;
    private Coroutine _overlayRoutine;

    public static BattleOutcomeCoordinator Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        // 兜底：上局失败/复活等流程可能残留非 1 的 timeScale，新局强制复位
        Time.timeScale = 1f;

        _gameLayer = FindObjectOfType<GameLayer>(true);
        _playerHealth = FindObjectOfType<PlayerHealth>(true);
        _reviveOfferUsed = false;
        _reviveFlowActive = false;
        _battleEnded = false;
        _hasBossDeathPosition = false;

        BattleRunMetrics.BeginBattle();
        BattleVictoryBossTracker.Reset();

        EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied, owner: this);
        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
        EventBus.Subscribe<BattleWavesCompletedEvent>(OnWavesCompleted, owner: this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        EventBus.Unsubscribe<BattleWavesCompletedEvent>(OnWavesCompleted);

        if (_overlayRoutine != null)
        {
            StopCoroutine(_overlayRoutine);
            _overlayRoutine = null;
        }
    }

    // ── 失败：玩家死亡 ──

    private void OnPlayerDied(PlayerDiedEvent _)
    {
        if (_battleEnded || _reviveFlowActive) return;

        if (!_reviveOfferUsed)
        {
            BeginReviveOffer();
            return;
        }

        StartCoroutine(DefeatSequence());
    }

    // ── 胜利：Boss 被击杀 ──

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (_battleEnded || _reviveFlowActive) return;
        if (e.enemy == null || e.enemy.GetComponent<LastWaveBossMarker>() == null) return;

        if (BattleVictoryBossTracker.TryRegisterKill())
        {
            _bossDeathPosition = e.position;
            _hasBossDeathPosition = true;
            StartCoroutine(VictorySequence());
        }
    }

    // ── 胜利（无 Boss 关卡）：波次刷完且场上无怪 ──

    private void OnWavesCompleted(BattleWavesCompletedEvent _)
    {
        if (_battleEnded || _reviveFlowActive) return;
        if (BattleVictoryBossTracker.UsesBossVictory) return;
        if (CombatTargetRegistry.CountActive("monster") > 0) return;

        StartCoroutine(VictorySequence());
    }

    // ── 胜利延迟结算 ──

    /// <summary>
    /// 镜头锁定 Boss 死亡位置 → 立刻慢动作 → 碎片飞散 → 弹出胜利面板。
    /// </summary>
    private IEnumerator VictorySequence()
    {
        _battleEnded = true;

        if (_playerHealth != null)
            _playerHealth.SetInvulnerable(true);

        // 镜头锁定 Boss 死亡位置并瞬间跳转
        var camFollow = FindObjectOfType<CameraFollow2D>(true);
        if (_hasBossDeathPosition && camFollow != null)
            camFollow.SnapAndLock(_bossDeathPosition);

        // timeScale 从 1 线性降到 victoryTimeScale
        float startTs = Time.timeScale;
        float targetTs = Mathf.Max(0.01f, victoryTimeScale);
        float t = 0f;
        float duration = Mathf.Max(0.3f, victoryDelaySeconds);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            Time.timeScale = Mathf.Lerp(startTs, targetTs, u);
            yield return null;
        }

        // 结算（先判定首次通关 → 记录进度 → 发放奖励）
        int kills = _gameLayer != null ? _gameLayer.CurrentKills : 0;
        float dur = BattleRunMetrics.GetBattleElapsedUnscaled();
        int levelId = SelectedLevelContext.LevelId;
        bool isFirstClear = !PlayerProfileService.Instance.HasCleared(levelId);
        var newUnlocks = TryRecordVictoryProgress(dur, kills);
        var rewardItems = AwardDropPoolRewards(levelId, isFirstClear);
        ShowResultUi(new GameResultViewModel
        {
            victory = true,
            battleDurationUnscaled = dur,
            killCount = kills,
            unlockedCharacters = newUnlocks,
            rewardItems = rewardItems,
        });

        // 恢复
        camFollow?.ClearOverride();
    }

    // ── 失败延迟结算 ──

    /// <summary>
    /// 死亡过渡：闪红 → 画面渐暗 → 弹出失败面板。
    /// 期间 Time.timeScale 从 1 缓降到低速，营造"慢慢倒下"的感觉。
    /// </summary>
    private IEnumerator DefeatSequence()
    {
        _battleEnded = true;

        EnsureScreenOverlay();

        // 画面缓速（不直接到 0，保留慢动作感）
        float startTs = Time.timeScale;
        float targetTs = 0.15f;

        // 阶段 1：闪红（正弦脉冲）
        float t = 0f;
        while (t < deathFlashDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Sin(t / Mathf.Max(0.01f, deathFlashDuration) * Mathf.PI) * 0.55f;
            _screenOverlay.color = new Color(1f, 0.05f, 0.05f, a);
            Time.timeScale = Mathf.Lerp(startTs, targetTs, t / deathFlashDuration);
            yield return null;
        }

        // 阶段 2：红 → 黑过渡
        float remain = Mathf.Max(0.3f, defeatDelaySeconds - deathFlashDuration);
        t = 0f;
        Color flashPeak = new Color(1f, 0.05f, 0.05f, 0.55f);
        Color blackTarget = new Color(0f, 0f, 0f, deathFadeTargetAlpha);

        while (t < remain)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / remain);
            _screenOverlay.color = Color.Lerp(flashPeak, blackTarget, u);
            Time.timeScale = Mathf.Lerp(targetTs, 0f, u);
            yield return null;
        }

        _screenOverlay.color = blackTarget;

        int kills = _gameLayer != null ? _gameLayer.CurrentKills : 0;
        float dur = BattleRunMetrics.GetBattleElapsedUnscaled();
        var defeatRewards = AwardDefeatGold();
        ShowResultUi(new GameResultViewModel
        {
            victory = false,
            battleDurationUnscaled = dur,
            killCount = kills,
            rewardItems = defeatRewards,
        });

        // 面板弹出后清除遮罩
        _screenOverlay.color = new Color(0f, 0f, 0f, 0f);
    }

    // ── 复活流程（不变）──

    private void BeginReviveOffer()
    {
        _reviveFlowActive = true;

        var payload = new GameRevivePanelPayload
        {
            countdownSeconds = 10f,
            adProvider = DefaultReviveAdProvider.Instance,
            onGiveUp = OnReviveGiveUp,
            onRevived = OnReviveAccepted
        };

        ShowReviveUi(payload);
    }

    private void OnReviveGiveUp()
    {
        if (_battleEnded) return;
        _reviveOfferUsed = true;
        _reviveFlowActive = false;
        UIManager.Instance.CloseTop();
        StartCoroutine(DefeatSequence());
    }

    private void OnReviveAccepted()
    {
        if (_battleEnded) return;
        _reviveOfferUsed = true;
        _reviveFlowActive = false;
        UIManager.Instance.CloseTop();

        if (_playerHealth == null)
            _playerHealth = FindObjectOfType<PlayerHealth>(true);
        _playerHealth?.ResetToFull();

        var hpBar = FindObjectOfType<PlayerWorldHpBar>(true);
        hpBar?.Refresh();

        if (Time.timeScale == 0f)
            Time.timeScale = 1f;
    }

    // ── 结算面板 ──

    private void ShowResultUi(GameResultViewModel vm)
    {
        UIManager.Instance.Open<GameResultPanel>(vm);
    }

    private void ShowReviveUi(GameRevivePanelPayload payload)
    {
        UIManager.Instance.Open<GameRevivePanel>(payload, UiOpenOptions.ModalDefault);
    }

    private static List<string> TryRecordVictoryProgress(float durationSec, int killCount)
    {
        if (!SelectedLevelContext.HasSelection) return new List<string>();
        PlayerProfileService.Instance.LoadOrCreate();

        int levelId = SelectedLevelContext.LevelId;
        if (!PlayerProfileService.Instance.IsLevelUnlocked(levelId)) return new List<string>();

        var health = FindObjectOfType<PlayerHealth>();
        int stars = health != null
            ? LevelStarRules.ComputeStars(health.Hp, health.MaxHp)
            : 1;
        PlayerProfileService.Instance.RecordVictory(levelId, durationSec, killCount, stars);
        return CharacterUnlockEvaluator.OnLevelCleared(levelId);
    }

    /// <summary>使用掉落池系统发放胜利奖励：星级池 + 首通池叠加 + 保底。</summary>
    private List<RewardItemEntry> AwardDropPoolRewards(int levelId, bool isFirstClear)
    {
        var result = new List<RewardItemEntry>();
        if (levelId <= 0) return result;

        TableManager.Instance.EnsureLoaded();

        if (!ChapterLevelCatalog.TryGetByLevelId(levelId, out var chapterRow))
        {
            Debug.LogWarning($"[DropManager] 关卡 {levelId} 在 ChapterLevel 表中未找到。");
            return result;
        }

        int stars = ComputeStarsFromLastVictory();
        int starPoolId = stars switch
        {
            3 => chapterRow.threeStarPoolId,
            2 => chapterRow.twoStarPoolId,
            _ => chapterRow.oneStarPoolId,
        };
        int firstClearPoolId = isFirstClear ? chapterRow.firstClearPoolId : 0;

        if (starPoolId <= 0 && firstClearPoolId <= 0)
            return result; // 无配池，不发放奖励

        // Roll 掉落池
        var drops = DropManager.RollMultiple(starPoolId, firstClearPoolId);

        // 保底
        var fragmentIds = DropManager.CollectFragmentIds(starPoolId, firstClearPoolId);
        int pityItemId = DropPityTracker.ReportAndCheck(levelId, drops, fragmentIds);
        if (pityItemId > 0)
        {
            int existingIdx = drops.FindIndex(d => d.itemId == pityItemId);
            if (existingIdx >= 0)
                drops[existingIdx] = new DropResult(pityItemId, drops[existingIdx].count + 1);
            else
                drops.Add(new DropResult(pityItemId, 1));
            Debug.Log($"[DropPity] 保底触发！关卡 {levelId}，强制掉落物品 {pityItemId}");
        }

        // 写入存档 + 构建展示列表
        foreach (var drop in drops)
        {
            if (drop.itemId == 1)
                PlayerProfileService.Instance.AddGold(drop.count);
            else
                PlayerProfileService.Instance.AddItem(drop.itemId, drop.count);

            var itemRow = TableManager.Instance.GetTableItem<ProtoTable.ItemTable>(drop.itemId) as ProtoTable.ItemTable;
            result.Add(new RewardItemEntry
            {
                itemId = drop.itemId,
                itemName = itemRow?.ItemName ?? (drop.itemId == 1 ? "金币" : $"物品{drop.itemId}"),
                iconPath = itemRow?.IconPath ?? "",
                count = drop.count,
                grade = itemRow?.Grade ?? 0,
                description = itemRow?.Description ?? "",
            });
        }

        if (result.Count > 0)
        {
            string tag = isFirstClear ? "首通" : "重复";
            if (firstClearPoolId > 0 && isFirstClear) tag += " + 首通奖励池";
            var sb = new System.Text.StringBuilder();
            foreach (var r in result) sb.Append($"{r.itemName}×{r.count} ");
            Debug.Log($"[DropManager] 关卡 {levelId} 奖励（{tag}, starPool={starPoolId}, firstPool={firstClearPoolId}, stars={stars}）：{sb}");
        }

        return result;
    }

    /// <summary>失败结算：按已完成波次发放金币。</summary>
    private List<RewardItemEntry> AwardDefeatGold()
    {
        var result = new List<RewardItemEntry>();
        int levelId = SelectedLevelContext.LevelId;
        if (levelId <= 0) return result;

        TableManager.Instance.EnsureLoaded();
        if (!ChapterLevelCatalog.TryGetByLevelId(levelId, out var chapterRow))
            return result;

        int goldPerWave = chapterRow.defeatGoldPerWave;
        if (goldPerWave <= 0) return result;

        var gameLayer = FindObjectOfType<GameLayer>(true);
        int completedWaves = 0;
        if (gameLayer != null)
            completedWaves = Mathf.Max(0, gameLayer.CurrentWave - 1);

        if (completedWaves <= 0) return result;

        int gold = goldPerWave * completedWaves;
        PlayerProfileService.Instance.AddGold(gold);

        // 查 ItemTable 获取金币图标 & 品级，保证与胜利结算展示一致
        var itemRow = TableManager.Instance.GetTableItem<ProtoTable.ItemTable>(1) as ProtoTable.ItemTable;
        result.Add(new RewardItemEntry
        {
            itemId = 1,
            itemName = itemRow?.ItemName ?? "金币",
            iconPath = itemRow?.IconPath ?? "",
            count = gold,
            grade = itemRow?.Grade ?? 0,
            description = itemRow?.Description ?? "",
        });

        Debug.Log($"[DropManager] 失败结算：完成 {completedWaves} 波，奖励 {gold} 金币（perWave={goldPerWave}）");
        return result;
    }

    private static int ComputeStarsFromLastVictory()
    {
        var health = FindObjectOfType<PlayerHealth>();
        if (health != null)
            return LevelStarRules.ComputeStars(health.Hp, health.MaxHp);
        return 1;
    }

    // ── 全屏遮罩（死亡过渡 + 波次/Boss 警告共用）──

    private void EnsureScreenOverlay()
    {
        if (_screenOverlay != null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        var overlayGo = new GameObject("ScreenFlashOverlay", typeof(RectTransform), typeof(Image));
        overlayGo.transform.SetParent(parent, false);
        var rt = overlayGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _screenOverlay = overlayGo.GetComponent<Image>();
        _screenOverlay.color = new Color(0f, 0f, 0f, 0f);
        _screenOverlay.raycastTarget = false;

        // 确保在最顶层（低于结算面板）
        Canvas overlayCanvas = overlayGo.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 900;
        overlayGo.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// 通用警告闪红（波次/Boss 等外部系统调用）。
    /// </summary>
    /// <param name="color">闪烁颜色</param>
    /// <param name="pulseCount">脉冲次数</param>
    /// <param name="duration">总时长（秒）</param>
    public void FlashWarning(Color color, int pulseCount, float duration)
    {
        if (_overlayRoutine != null)
            StopCoroutine(_overlayRoutine);
        _overlayRoutine = StartCoroutine(FlashWarningRoutine(color, pulseCount, duration));
    }

    private IEnumerator FlashWarningRoutine(Color color, int pulseCount, float duration)
    {
        EnsureScreenOverlay();

        int pulses = Mathf.Max(1, pulseCount);
        float pulseLen = duration / (pulses * 2f); // 一亮一暗算一个脉冲

        for (int i = 0; i < pulses; i++)
        {
            // 亮
            float t = 0f;
            while (t < pulseLen)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(0f, color.a, Mathf.Clamp01(t / pulseLen));
                _screenOverlay.color = new Color(color.r, color.g, color.b, a);
                yield return null;
            }
            // 暗
            t = 0f;
            while (t < pulseLen)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(color.a, 0f, Mathf.Clamp01(t / pulseLen));
                _screenOverlay.color = new Color(color.r, color.g, color.b, a);
                yield return null;
            }
        }

        _screenOverlay.color = new Color(0f, 0f, 0f, 0f);
        _overlayRoutine = null;
    }
}
