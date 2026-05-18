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

    [Tooltip("暂停按钮（点击切换 Time.timeScale 0/1）")]
    public Button pauseButton;

    [Header("Energy → Card Selection")]
    [Tooltip("能量进度条（预制体 SliderProgress）")]
    public Slider energyProgressSlider;

    [Tooltip("能量进度文字（预制体 Textprogress，如 70%）")]
    public TextMeshProUGUI energyProgressText;

    [Tooltip("留空则在场景中查找 PlayerEnergy")]
    public PlayerEnergy playerEnergy;

    [Header("Game Data")]
    public int targetKills = 100;

    [Tooltip("倒计时总时长（秒）。例如 300 表示 5 分钟。")]
    public int countdownSeconds = 300;

    private int _kills;
    private float _timeLeft;
    private bool _paused;
    private PlayerEnergy _playerEnergy;

    public int CurrentKills => _kills;
    public int TargetKills => targetKills;

    private void Start()
    {
        
        // 如果没有拖 UI 引用，自动创建一套基础 UI，便于快速跑起来
        if (killText == null || timerText == null || pauseButton == null)
            BuildMinimalUIIfMissing();

        _timeLeft = Mathf.Max(0, countdownSeconds);
        RefreshKillText();
        RefreshTimerText();

        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);

        ResolveEnergyProgressRefs();
        BindPlayerEnergy();
        RefreshEnergyProgress();

        // 订阅怪物死亡事件：更新击杀数
        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
        EventBus.Subscribe<CardSelectionEndedEvent>(OnCardSelectionEnded, owner: this);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        EventBus.Unsubscribe<CardSelectionEndedEvent>(OnCardSelectionEnded);
        if (_playerEnergy != null)
            _playerEnergy.OnEnergyChanged.RemoveListener(OnPlayerEnergyChanged);
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        // 你要求：死亡加击杀数
        if (e.rewardKillCount > 0)
            AddKill(e.rewardKillCount);
    }

    private void Update()
    {
        // 倒计时：使用 unscaledDeltaTime，暂停后仍会走 UI 更新逻辑（但我们会在 paused 时不减少时间）
        if (_paused) return;
        if (_timeLeft <= 0f) return;

        _timeLeft -= Time.deltaTime;
        if (_timeLeft < 0f) _timeLeft = 0f;
        RefreshTimerText();
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
    public void SetCountdown(int seconds)
    {
        _timeLeft = Mathf.Max(0, seconds);
        RefreshTimerText();
    }

    public void TogglePause()
    {
        _paused = !_paused;
        Time.timeScale = _paused ? 0f : 1f;

        // 可选：更新按钮文字
        if (pauseButton != null)
        {
            var t = pauseButton.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.text = _paused ? "Resume" : "Pause";
        }
    }

    private void RefreshKillText()
    {
        if (killText == null) return;
        killText.text = $"{_kills}/{targetKills}";
    }

    private void RefreshTimerText()
    {
        if (timerText == null) return;

        int total = Mathf.CeilToInt(_timeLeft);
        int mm = total / 60;
        int ss = total % 60;
        timerText.text = $"{mm:00}:{ss:00}";
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

    /// <summary>根据 <see cref="PlayerEnergy"/> 刷新选卡能量进度条与文字。</summary>
    public void RefreshEnergyProgress()
    {
        if (_playerEnergy == null)
            return;

        int need = _playerEnergy.EnergyRequiredForNextCard;
        int cur = Mathf.Max(0, _playerEnergy.energy);
        float ratio = Mathf.Clamp01((float)cur / need);

        if (energyProgressSlider != null)
        {
            energyProgressSlider.minValue = 0f;
            energyProgressSlider.maxValue = 1f;
            energyProgressSlider.value = ratio;
        }

        if (energyProgressText != null)
            energyProgressText.text = $"{Mathf.RoundToInt(ratio * 100f)}%";
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

        if (pauseButton == null)
        {
            pauseButton = CreateButton("PauseButton", canvas.transform, new Vector2(-10, 10), TextAnchor.LowerRight);
            var t = pauseButton.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.text = "Pause";
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

