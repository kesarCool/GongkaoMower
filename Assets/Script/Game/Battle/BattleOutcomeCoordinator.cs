using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 局内胜负判定与拉起结算 UI。
/// - 本组件<strong>不是</strong>结算面板的父节点：父节点由 <see cref="UIManager"/> 的 <c>stackRoot</c>（优先）
///   或 <see cref="resultParentOverride"/> / 场景内首个 <see cref="Canvas"/>（回退）决定。
/// - 结算 UI 优先走 <see cref="UIManager.Open{T}"/>（需在场景的 UIManager 上注册 <see cref="GameResultPanel"/> 预制体）；
///   无实例或未注册时再 <c>Instantiate</c> 回退，避免 Game 场景未挂 UIManager 时完全打不开。
/// </summary>
[DefaultExecutionOrder(-40)]
public sealed class BattleOutcomeCoordinator : MonoBehaviour
{
    private const string GameResultPrefabAssetPath = "Assets/Prefab/Result/GameResultPanel.prefab";

    [SerializeField] private GameObject gameResultPanelPrefab;

    [Tooltip("结算实例父节点；空则挂到场景中第一个 Screen Space Canvas 下")]
    [SerializeField] private RectTransform resultParentOverride;

    private GameLayer _gameLayer;
    private PlayerHealth _playerHealth;
    private bool _spawnWavesFullyFinished;
    private bool _battleEnded;
    private GameObject _resultUiInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateInGameScene()
    {
        Scene s = SceneManager.GetActiveScene();
        if (!s.IsValid() || s.name != "Game") return;

        var all = FindObjectsOfType<BattleOutcomeCoordinator>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c != null && c.isActiveAndEnabled)
                return;
        }

        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c == null) continue;
            c.gameObject.SetActive(true);
            c.enabled = true;
            return;
        }

        var go = new GameObject(nameof(BattleOutcomeCoordinator));
        go.AddComponent<BattleOutcomeCoordinator>();
    }

    private void Awake()
    {
#if UNITY_EDITOR
        if (gameResultPanelPrefab == null)
            gameResultPanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameResultPrefabAssetPath);
#endif
        if (gameResultPanelPrefab == null)
            gameResultPanelPrefab = Resources.Load<GameObject>("GameResultPanel");
    }

    private void OnEnable()
    {
        _gameLayer = FindObjectOfType<GameLayer>(true);
        TryCachePlayerHealth();

        if (SelectedLevelContext.HasSelection && RoguelikeCardManager.Instance != null)
            RoguelikeCardManager.Instance.CurrentLevel = SelectedLevelContext.LevelId;

        BattleRunMetrics.BeginBattle();

        if (!AnyRelevantSpawnerInScene())
            Debug.LogWarning("[BattleOutcomeCoordinator] 场景中未找到已启用的 SpawnerWaves，将无法通过「波次结束」判定胜利。");

        ApplyKillQuotaToHud();

        EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied, owner: this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    private void Update()
    {
        if (_battleEnded) return;

        TryCachePlayerHealth();

        if (_playerHealth != null && !_playerHealth.IsAlive)
        {
            EndBattleDefeat();
            return;
        }

        RefreshSpawnWavesFinishedFromSpawners();
        if (!_spawnWavesFullyFinished) return;

        if (CountMonstersAlive() > 0) return;

        EndBattleVictory();
    }

    private static bool AnyRelevantSpawnerInScene()
    {
        foreach (var s in FindObjectsOfType<SpawnerWaves>(true))
        {
            if (IsSpawnerRelevantForWaveProgress(s))
                return true;
        }

        return false;
    }

    private static bool IsSpawnerRelevantForWaveProgress(SpawnerWaves s)
    {
        return s != null && s.isActiveAndEnabled && s.enabled;
    }

    /// <summary>
    /// 轮询兜底：避免 BattleWavesCompletedEvent 在订阅前发出、或 EventBus 漏处理时卡死。
    /// </summary>
    private void RefreshSpawnWavesFinishedFromSpawners()
    {
        if (_spawnWavesFullyFinished) return;

        SpawnerWaves[] arr = FindObjectsOfType<SpawnerWaves>(true);
        if (arr == null || arr.Length == 0) return;

        int relevant = 0;
        for (int i = 0; i < arr.Length; i++)
        {
            SpawnerWaves s = arr[i];
            if (!IsSpawnerRelevantForWaveProgress(s)) continue;
            relevant++;
            if (!s.HasReleasedWaveCompletionSignal)
                return;
        }

        if (relevant > 0)
            _spawnWavesFullyFinished = true;
    }

    private void TryCachePlayerHealth()
    {
        if (_playerHealth != null)
            return;

        _playerHealth = FindObjectOfType<PlayerHealth>(true);
    }

    private void ApplyKillQuotaToHud()
    {
        if (_gameLayer == null) return;

        int levelId = RoguelikeCardManager.Instance != null
            ? RoguelikeCardManager.Instance.CurrentLevel
            : 1;

        int quota = LevelWaveKillQuota.SumTotalMonstersForLevel(levelId);
        if (quota > 0)
            _gameLayer.targetKills = quota;
    }

    private void OnPlayerDied(PlayerDiedEvent e)
    {
        if (_battleEnded) return;
        EndBattleDefeat();
    }

    private static int CountMonstersAlive()
    {
        try
        {
            GameObject[] arr = GameObject.FindGameObjectsWithTag("monster");
            return arr != null ? arr.Length : 0;
        }
        catch (UnityException)
        {
            return 0;
        }
    }

    private void EndBattleVictory()
    {
        if (_battleEnded) return;
        if (_playerHealth != null && !_playerHealth.IsAlive) return;

        _battleEnded = true;
        int kills = _gameLayer != null ? _gameLayer.CurrentKills : 0;
        float dur = BattleRunMetrics.GetBattleElapsedUnscaled();
        ShowResultUi(new GameResultViewModel { victory = true, battleDurationUnscaled = dur, killCount = kills });
    }

    private void EndBattleDefeat()
    {
        if (_battleEnded) return;
        _battleEnded = true;

        int kills = _gameLayer != null ? _gameLayer.CurrentKills : 0;
        float dur = BattleRunMetrics.GetBattleElapsedUnscaled();
        ShowResultUi(new GameResultViewModel { victory = false, battleDurationUnscaled = dur, killCount = kills });
    }

    private void ShowResultUi(GameResultViewModel vm)
    {
        if (UIManager.Instance != null)
        {
            GameResultPanel managed = UIManager.Instance.Open<GameResultPanel>(vm);
            if (managed != null)
            {
                _resultUiInstance = null;
                return;
            }

            Debug.LogWarning("[BattleOutcomeCoordinator] UIManager 存在但未注册 GameResultPanel，将回退为 Instantiate。");
        }

        if (gameResultPanelPrefab == null)
        {
            Debug.LogError("[BattleOutcomeCoordinator] 未配置 gameResultPanelPrefab（请在 UIManager 注册 GameResultPanel，或拖入预制体 / 放入 Resources）。");
            return;
        }

        Transform parent = resultParentOverride;
        if (parent == null)
        {
            Canvas c = FindObjectOfType<Canvas>(true);
            parent = c != null ? c.transform as RectTransform : null;
        }

        if (parent == null)
        {
            Debug.LogError("[BattleOutcomeCoordinator] 找不到 Canvas，无法显示结算。");
            return;
        }

        if (_resultUiInstance != null)
            Destroy(_resultUiInstance);

        _resultUiInstance = Instantiate(gameResultPanelPrefab, parent, false);
        var panel = _resultUiInstance.GetComponent<GameResultPanel>();
        if (panel == null)
        {
            Debug.LogError("[BattleOutcomeCoordinator] GameResultPanel 预制体缺少 GameResultPanel 组件。");
            return;
        }

        panel.OnOpen(vm);
    }
}
