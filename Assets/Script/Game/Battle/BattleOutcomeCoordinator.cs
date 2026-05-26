using UnityEngine;

/// <summary>
/// 局内胜负判定 + 复活流程 + 结算入口。纯事件驱动，不轮询。
/// 需要场景已有 UIManager 并注册 GameResultPanel / GameRevivePanel。
/// </summary>
[DefaultExecutionOrder(-40)]
public sealed class BattleOutcomeCoordinator : MonoBehaviour
{
    private GameLayer _gameLayer;
    private PlayerHealth _playerHealth;
    private bool _battleEnded;
    private bool _reviveOfferUsed;
    private bool _reviveFlowActive;

    private void OnEnable()
    {
        _gameLayer = FindObjectOfType<GameLayer>(true);
        _playerHealth = FindObjectOfType<PlayerHealth>(true);
        _reviveOfferUsed = false;
        _reviveFlowActive = false;
        _battleEnded = false;

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

        EndBattleDefeat();
    }

    // ── 胜利：Boss 被击杀 ──

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (_battleEnded || _reviveFlowActive) return;
        if (e.enemy == null || e.enemy.GetComponent<LastWaveBossMarker>() == null) return;

        if (BattleVictoryBossTracker.TryRegisterKill())
            EndBattleVictory();
    }

    // ── 胜利（无 Boss 关卡）：波次刷完且场上无怪 ──

    private void OnWavesCompleted(BattleWavesCompletedEvent _)
    {
        if (_battleEnded || _reviveFlowActive) return;
        if (BattleVictoryBossTracker.UsesBossVictory) return;
        if (CombatTargetRegistry.CountActive("monster") > 0) return;

        EndBattleVictory();
    }

    // ── 复活流程 ──

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
        EndBattleDefeat();
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

    // ── 结算 ──

    private void EndBattleVictory()
    {
        if (_battleEnded) return;
        _battleEnded = true;

        int kills = _gameLayer != null ? _gameLayer.CurrentKills : 0;
        float dur = BattleRunMetrics.GetBattleElapsedUnscaled();
        TryRecordVictoryProgress(dur, kills);
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
        UIManager.Instance.Open<GameResultPanel>(vm);
    }

    private void ShowReviveUi(GameRevivePanelPayload payload)
    {
        UIManager.Instance.Open<GameRevivePanel>(payload, UiOpenOptions.ModalDefault);
    }

    private static void TryRecordVictoryProgress(float durationSec, int killCount)
    {
        if (!SelectedLevelContext.HasSelection) return;
        PlayerProfileService.Instance.LoadOrCreate();

        int levelId = SelectedLevelContext.LevelId;
        if (!PlayerProfileService.Instance.IsLevelUnlocked(levelId)) return;

        var health = FindObjectOfType<PlayerHealth>();
        int stars = health != null
            ? LevelStarRules.ComputeStars(health.Hp, health.MaxHp)
            : 1;
        PlayerProfileService.Instance.RecordVictory(levelId, durationSec, killCount, stars);
    }
}
