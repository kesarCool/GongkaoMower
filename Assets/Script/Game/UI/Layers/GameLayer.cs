using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameLayer
/// - 挂在 UI Canvas（Screen Space - Overlay）下，用于显示“击杀数 / 目标”、“倒计时”、“暂停按钮”等基础 HUD。
///
/// 说明：
/// - 这个脚本本身不负责击杀判定，只负责“显示”和“提供简单 API 供其它脚本更新 UI”。
/// - 你可以在 Inspector 里把 Text/Button 拖进来；如果不拖，也可以让脚本在 Start 时自动创建一套最简 UI。
/// </summary>
[DisallowMultipleComponent]
public class GameLayer : MonoBehaviour
{
    [Header("UI Refs (Optional)")]
    [Tooltip("显示击杀数：格式 '当前击杀/目标'（例如 12/100）")]
    public TextMeshProUGUI killText;

    [Tooltip("显示倒计时：格式 '分钟:秒'（例如 05:30）")]
    public TextMeshProUGUI timerText;

    [Tooltip("显示当前爆兵波次：波次 n/m（预制体 Textwave）")]
    public TextMeshProUGUI waveText;

    [Tooltip("显示当前关卡（预制体 TextLevel，读选关上下文）")]
    public TextMeshProUGUI levelText;

    [Header("Energy → Card Selection")]
    [Tooltip("能量进度条（预制体 SliderProgress），Boss 出场时隐藏")]
    public Slider energyProgressSlider;

    [Tooltip("能量进度文字（预制体 Textprogress，如 70%）")]
    public TextMeshProUGUI energyProgressText;

    [Header("Boss HP")]
    [Tooltip("Boss 血条（Slider），Boss 出场时显示，与能量条互斥")]
    public Slider bossHpSlider;

    [Tooltip("Boss 名字（TextMeshPro），显示在血条左侧")]
    public TextMeshProUGUI bossHpNameText;

    [Tooltip("Boss 血量数字（如 450/500）")]
    public TextMeshProUGUI bossHpValueText;

    [Tooltip("留空则在场景中查找 PlayerEnergy")]
    public PlayerEnergy playerEnergy;

    [Header("Game Data")]
    public int targetKills = 100;

    [Tooltip("正计时上限（秒，0=无限）。到达后不变，不触发任何事件。")]
    public int maxSeconds;

    private int _kills;
    private float _timeElapsed;
    private bool _paused;
    private PlayerEnergy _playerEnergy;
    private int _currentWave;
    private int _totalWaves;
    private EnemyBase _currentBoss;

    public int CurrentKills => _kills;
    public int TargetKills => targetKills;
    public int CurrentWave => _currentWave;

    private void Start()
    {
        AudioService.Ensure().StartCoroutine(AudioService.Ensure().LoadGroupAsync(AudioLoadGroup.Battle));

        // 如果没有拖 UI 引用，自动创建一套基础 UI，便于快速跑起来
        if (killText == null || timerText == null)
            BuildMinimalUIIfMissing();

        _timeElapsed = 0f;
        RefreshKillText();
        RefreshTimerText();

        ResolveWaveTextRef();
        ResolveLevelTextRef();
        InitWaveDisplayFromSpawner();
        ApplyKillQuotaFromTable();
        RefreshWaveText();
        RefreshLevelText();

        ResolveEnergyProgressRefs();
        BindPlayerEnergy();
        RefreshEnergyProgress();

        // 订阅怪物死亡事件：更新击杀数
        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
        EventBus.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged, owner: this);
        EventBus.Subscribe<CardSelectionEndedEvent>(OnCardSelectionEnded, owner: this);
        EventBus.Subscribe<BattleWaveChangedEvent>(OnBattleWaveChanged, owner: this);

        HideBossHp();
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        EventBus.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
        EventBus.Unsubscribe<CardSelectionEndedEvent>(OnCardSelectionEnded);
        EventBus.Unsubscribe<BattleWaveChangedEvent>(OnBattleWaveChanged);
        if (_playerEnergy != null)
            _playerEnergy.OnEnergyChanged.RemoveListener(OnPlayerEnergyChanged);
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        if (e.rewardKillCount > 0)
            AddKill(e.rewardKillCount);

        if (_currentBoss == e.enemy)
        {
            _currentBoss = null;
            HideBossHp();
            RefreshEnergyProgress();
        }
    }

    private void OnEnemyDamaged(EnemyDamagedEvent e)
    {
        // 检测 Boss：有 LastWaveBossMarker 标记
        if (_currentBoss == null && e.enemy != null)
        {
            if (e.enemy.GetComponent<LastWaveBossMarker>() != null)
            {
                _currentBoss = e.enemy;
                ShowBossHp(e.enemy.EnemyName);
            }
        }

        if (_currentBoss == e.enemy)
            RefreshBossHp();
    }

    private void ShowBossHp(string bossName)
    {
        if (energyProgressSlider != null)
            energyProgressSlider.gameObject.SetActive(false);
        if (energyProgressText != null)
            energyProgressText.gameObject.SetActive(false);

        if (bossHpSlider != null)
            bossHpSlider.gameObject.SetActive(true);
        if (bossHpNameText != null)
        {
            bossHpNameText.text = bossName ?? "Boss";
            bossHpNameText.gameObject.SetActive(true);
        }
        if (bossHpValueText != null)
            bossHpValueText.gameObject.SetActive(true);

        bossHpSlider.minValue = 0f;
        bossHpSlider.maxValue = 1f;
        RefreshBossHp();
    }

    private void HideBossHp()
    {
        if (bossHpSlider != null)
            bossHpSlider.gameObject.SetActive(false);
        if (bossHpNameText != null)
            bossHpNameText.gameObject.SetActive(false);
        if (bossHpValueText != null)
            bossHpValueText.gameObject.SetActive(false);

        if (energyProgressSlider != null)
            energyProgressSlider.gameObject.SetActive(true);
        if (energyProgressText != null)
            energyProgressText.gameObject.SetActive(true);
    }

    private void RefreshBossHp()
    {
        if (_currentBoss == null) return;

        float ratio = Mathf.Clamp01(_currentBoss.Hp / Mathf.Max(1f, _currentBoss.MaxHp));
        if (bossHpSlider != null)
            bossHpSlider.value = ratio;
        if (bossHpValueText != null)
            bossHpValueText.text = $"{Mathf.CeilToInt(_currentBoss.Hp)}/{Mathf.CeilToInt(_currentBoss.MaxHp)}";
    }

    private void Update()
    {
        if (!_paused)
        {
            if (maxSeconds <= 0 || _timeElapsed < maxSeconds)
            {
                _timeElapsed += Time.deltaTime;
                RefreshTimerText();
            }
        }
    }

    /// <summary>
    /// 增加击杀数（外部在敌人死亡时调用）
    /// </summary>
    public void AddKill(int amount = 1)
    {
        _kills += amount;
        if (_kills < 0) _kills = 0;
        RefreshKillText();
    }

    /// <summary>
    /// 直接设置击杀数
    /// </summary>
    public void SetKills(int kills)
    {
        _kills = Mathf.Max(0, kills);
        RefreshKillText();
    }

    /// <summary>
    /// 重置/设置倒计时（秒）
    /// </summary>
    private void RefreshKillText()
    {
        if (killText == null) return;
        killText.text = $"{_kills}/{targetKills}";
    }

    private void RefreshTimerText()
    {
        if (timerText == null) return;

        int total = Mathf.FloorToInt(_timeElapsed);
        int mm = total / 60;
        int ss = total % 60;
        timerText.text = $"{mm:00}:{ss:00}";
    }

    private void ResolveWaveTextRef()
    {
        if (waveText != null)
            return;

        Transform t = transform.Find("Textwave");
        if (t != null)
            waveText = t.GetComponent<TextMeshProUGUI>();
    }

    private void ResolveLevelTextRef()
    {
        if (levelText != null)
            return;

        Transform t = transform.Find("TextLevel");
        if (t != null)
            levelText = t.GetComponent<TextMeshProUGUI>();
    }

    private void RefreshLevelText()
    {
        if (levelText == null)
            return;

        BattleLevelContext.LogMissingSelectionOnce(nameof(GameLayer));
        levelText.text = BattleLevelContext.GetDisplayText();
    }

    private void ApplyKillQuotaFromTable()
    {
        int levelId = BattleLevelContext.LevelId;
        int quota = LevelWaveKillQuota.SumTotalMonstersForLevel(levelId);
        if (quota > 0)
            targetKills = quota;
    }

    private void InitWaveDisplayFromSpawner()
    {
        var spawner = FindObjectOfType<SpawnerWaves>();
        if (spawner == null)
            return;

        _totalWaves = spawner.GetConfiguredWaveCount();
        _currentWave = 0;
    }

    private void OnBattleWaveChanged(BattleWaveChangedEvent e)
    {
        _currentWave = Mathf.Max(0, e.currentWave);
        _totalWaves = Mathf.Max(0, e.totalWaves);
        RefreshWaveText();
    }

    private void RefreshWaveText()
    {
        if (waveText == null)
            return;

        if (_totalWaves <= 0)
        {
            waveText.text = string.Empty;
            return;
        }

        int displayCurrent = _currentWave > 0 ? _currentWave : 0;
        waveText.text = $"波次{displayCurrent}/{_totalWaves}";
    }

    private void ResolveEnergyProgressRefs()
    {
        if (energyProgressSlider == null)
        {
            Transform t = transform.Find("SliderProgress");
            if (t != null)
                energyProgressSlider = t.GetComponent<Slider>();
        }

        if (energyProgressSlider != null)
            energyProgressSlider.interactable = false;

        if (energyProgressText == null && energyProgressSlider != null)
        {
            Transform t = energyProgressSlider.transform.Find("Textprogress");
            if (t != null)
                energyProgressText = t.GetComponent<TextMeshProUGUI>();
        }
    }

    private void BindPlayerEnergy()
    {
        _playerEnergy = playerEnergy != null ? playerEnergy : FindObjectOfType<PlayerEnergy>();
        if (_playerEnergy == null)
            return;

        _playerEnergy.OnEnergyChanged.AddListener(OnPlayerEnergyChanged);
    }

    private void OnPlayerEnergyChanged(int _)
    {
        RefreshEnergyProgress();
    }

    private void OnCardSelectionEnded(CardSelectionEndedEvent _)
    {
        RefreshEnergyProgress();
    }

    /// <summary>
    /// 刷新能量显示：等级 = 已完成选卡次数+1，进度 = 当前能量 / 下一级所需。
    /// </summary>
    public void RefreshEnergyProgress()
    {
        if (_playerEnergy == null)
            return;

        int curEnergy = Mathf.Max(0, _playerEnergy.energy);
        int need = _playerEnergy.EnergyRequiredForNextCard;
        int displayLv = _playerEnergy.CompletedCardSelectionCount + 1;
        float ratio = Mathf.Clamp01((float)curEnergy / Mathf.Max(1, need));

        if (energyProgressSlider != null)
        {
            energyProgressSlider.minValue = 0f;
            energyProgressSlider.maxValue = 1f;
            energyProgressSlider.value = ratio;
        }

        if (energyProgressText != null)
            energyProgressText.text = $"Lv{displayLv}";
    }

    /// <summary>
    /// 自动创建一套简单 HUD（左上 kills、右上 timer、右下 pause）
    /// 注意：如果你已经有漂亮的 UI 预制体/布局，建议自己做 Canvas 并把引用拖进来，而不是用这个自动创建。
    /// </summary>
    private void BuildMinimalUIIfMissing()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("GameLayer: No Canvas found in parents. Please put this under a UI Canvas.");
            return;
        }

        if (killText == null)
        {
            killText = CreateHudText("KillText", canvas.transform, new Vector2(10, -10), TextAnchor.UpperLeft);
            killText.text = "0/100";
        }

        if (timerText == null)
        {
            timerText = CreateHudText("TimerText", canvas.transform, new Vector2(-10, -10), TextAnchor.UpperRight);
            timerText.text = "00:00";
        }

    }

    private TextMeshProUGUI CreateHudText(string name, Transform parent, Vector2 anchoredPos, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 60);

        switch (anchor)
        {
            case TextAnchor.UpperLeft:
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                break;
            case TextAnchor.UpperRight:
                rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 1);
                break;
            case TextAnchor.LowerLeft:
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
                break;
            case TextAnchor.LowerRight:
                rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(1, 0);
                break;
        }

        rt.anchoredPosition = anchoredPos;

        var txt = go.GetComponent<TextMeshProUGUI>();
        txt.fontSize = 32;
        txt.alignment = UITextMeshProUtil.ToAlignment(anchor);
        txt.color = Color.white;
        txt.raycastTarget = false;
        BattleChineseFontRuntime.ApplyToTMP(txt);
        return txt;
    }

    private Button CreateButton(string name, Transform parent, Vector2 anchoredPos, TextAnchor corner)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(220, 80);

        // 放到右下角
        if (corner == TextAnchor.LowerRight)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
        }

        rt.anchoredPosition = anchoredPos;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.5f);

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var t = textGo.GetComponent<TextMeshProUGUI>();
        t.fontSize = 28;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        BattleChineseFontRuntime.ApplyToTMP(t);

        return go.GetComponent<Button>();
    }
}

