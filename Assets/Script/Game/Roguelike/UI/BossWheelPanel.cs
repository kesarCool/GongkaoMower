using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Boss 击杀转盘面板（UIPanelBase 子类）。
///
/// 【UI 接线说明】
/// 1. 16 个环形卡槽：wheelSlots[0..15]，每个挂 WheelSlotCell
///    - Image icon / TMP_Text levelText / GameObject highlightMark
///    - 极坐标排列：角度步长 = 360°/16 = 22.5°，i 从顶部顺时针
/// 2. spinButton + spinButtonText — 旋转按钮
/// 3. skipAnimationToggle — 跳过动画开关（PlayerPrefs 持久化）
/// 4. resultGroup + resultText + confirmButton — 结算弹窗
/// </summary>
public class BossWheelPanel : UIPanelBase
{
    [Header("环形卡槽")]
    [Tooltip("按顺时针排列的槽位（建议 16 个）")]
    public WheelSlotCell[] wheelSlots;

    [Header("按钮")]
    public Button spinButton;
    public TextMeshProUGUI spinButtonText;

    [Header("跳过动画")]
    public Toggle skipAnimationToggle;

    [Header("结算")]
    public GameObject resultGroup;
    [Tooltip("奖励卡展示（最多 3 张，对应 1~3 张中奖卡）")]
    public WheelRewardSkillCell[] rewardCells;
    public Button confirmButton;

    [Header("动画参数")]
    [Tooltip("Phase1 快转一圈总时长（秒），16 槽建议 3~4s")]
    public float spinDuration = 3.5f;
    [Tooltip("Phase2 每张中奖卡停留时长（秒）")]
    public float stopDuration = 0.5f;
    [Tooltip("Phase2 到下一张中奖卡之前快转圈数（减速感）")]
    [Range(0, 3)]
    public int extraLapsBeforeWinner = 1;

    private const string SkipAnimKey = "boss_wheel_skip_animation";

    private WheelSlotData[] _slots;
    private int[] _winningIndices;
    private System.Action _onSpinComplete;
    private System.Action _onClose;
    private bool _hasSpun;
    private bool _resultShown;
    private Coroutine _spinRoutine;

    private void Awake()
    {
        GameLog.Info("[BossWheelPanel] Awake");

        if (spinButton != null)
            spinButton.onClick.AddListener(OnSpinClicked);

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
            confirmButton.gameObject.SetActive(false);
        }

        if (skipAnimationToggle != null)
        {
            skipAnimationToggle.isOn = PlayerPrefs.GetInt(SkipAnimKey, 0) == 1;
            skipAnimationToggle.onValueChanged.AddListener(OnSkipToggleChanged);
        }

        if (resultGroup != null)
            resultGroup.SetActive(false);
    }

    public override void OnOpen(object payload)
    {
        GameLog.Info("[BossWheelPanel] OnOpen");

        var p = payload as BossWheelOpenPayload;
        if (p == null)
        {
            Debug.LogError("[BossWheelPanel] OnOpen 需要 BossWheelOpenPayload");
            return;
        }

        _slots = p.Slots;
        _winningIndices = p.WinningIndices;
        _onSpinComplete = p.OnSpinComplete;
        _onClose = p.OnClose;
        _hasSpun = false;
        _resultShown = false;

        GameLog.Info($"[BossWheelPanel] slots={_slots?.Length ?? 0} winners=[{string.Join(",", _winningIndices ?? new int[0])}] wheelCount={wheelSlots?.Length ?? 0}");

        BindSlots();
        ClearAllHighlights();

        if (resultGroup != null)
            resultGroup.SetActive(false);

        if (spinButton != null)
        {
            spinButton.gameObject.SetActive(true);
            spinButton.interactable = true;
        }
        if (skipAnimationToggle != null)
            skipAnimationToggle.gameObject.SetActive(true);
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(false);
        if (spinButtonText != null)
            spinButtonText.text = "开   始";

        GameLog.Info("[BossWheelPanel] OnOpen done");
    }

    public override void OnClose()
    {
        GameLog.Info("[BossWheelPanel] OnClose");
        if (_spinRoutine != null)
        {
            StopCoroutine(_spinRoutine);
            _spinRoutine = null;
        }
        base.OnClose();
    }

    private void BindSlots()
    {
        if (wheelSlots == null || _slots == null)
        {
            GameLog.Warning("[BossWheelPanel] BindSlots: null ref");
            return;
        }

        int bound = 0;
        for (int i = 0; i < wheelSlots.Length; i++)
        {
            if (wheelSlots[i] == null) continue;

            if (i < _slots.Length)
            {
                wheelSlots[i].Bind(_slots[i]);
                bound++;
            }
            else
            {
                wheelSlots[i].gameObject.SetActive(false);
            }
        }
        GameLog.Info($"[BossWheelPanel] BindSlots: bound={bound}/{wheelSlots.Length}");
    }

    private void ClearAllHighlights()
    {
        if (wheelSlots == null) return;
        for (int i = 0; i < wheelSlots.Length; i++)
        {
            if (wheelSlots[i] != null)
                wheelSlots[i].SetHighlight(false);
        }
    }

    #region 按钮回调

    private void OnSpinClicked()
    {
        GameLog.Info($"[BossWheelPanel] OnSpinClicked hasSpun={_hasSpun}");
        if (_hasSpun) return;
        _hasSpun = true;

        UiClickSound.Play();

        if (spinButton != null)
            spinButton.gameObject.SetActive(false);

        // 动画开始后隐藏这些控件
        if (skipAnimationToggle != null)
            skipAnimationToggle.gameObject.SetActive(false);
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(false);

        bool skip = skipAnimationToggle != null && skipAnimationToggle.isOn;
        GameLog.Info($"[BossWheelPanel] skip={skip}");

        if (skip)
            SkipAndShowResult();
        else
        {
            if (_spinRoutine != null) StopCoroutine(_spinRoutine);
            _spinRoutine = StartCoroutine(SpinRoutine());
        }
    }

    private void OnConfirmClicked()
    {
        GameLog.Info("[BossWheelPanel] OnConfirmClicked — closing");
        UiClickSound.Play();

        if (UIManager.Instance != null)
            UIManager.Instance.CloseTop();
        else
            GameLog.Warning("[BossWheelPanel] OnConfirmClicked: UIManager.Instance null!");

        _onClose?.Invoke();
    }

    private void OnSkipToggleChanged(bool on)
    {
        PlayerPrefs.SetInt(SkipAnimKey, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    #endregion

    #region 动画

    private void SkipAndShowResult()
    {
        GameLog.Info($"[BossWheelPanel] SkipAndShowResult: {_winningIndices?.Length ?? 0} winners");
        foreach (var idx in _winningIndices)
        {
            if (idx >= 0 && idx < wheelSlots.Length && wheelSlots[idx] != null)
                wheelSlots[idx].SetHighlight(true);
        }
        ShowResult();
    }

    /// <summary>
    /// Phase 1：高亮顺时针快转一圈（逐个 cell 闪亮）。
    /// Phase 2：从当前位置走到每张中奖卡，停住高亮。
    /// </summary>
    private IEnumerator SpinRoutine()
    {
        GameLog.Info("[BossWheelPanel] SpinRoutine start");

        int n = wheelSlots?.Length ?? 0;
        if (n == 0)
        {
            GameLog.Warning("[BossWheelPanel] SpinRoutine: no slots, skip");
            SkipAndShowResult();
            yield break;
        }

        // 收集有效槽位下标
        var validIndices = new System.Collections.Generic.List<int>(n);
        for (int i = 0; i < n; i++)
        {
            if (wheelSlots[i] != null && wheelSlots[i].gameObject.activeInHierarchy)
                validIndices.Add(i);
        }

        GameLog.Info($"[BossWheelPanel] SpinRoutine: valid={validIndices.Count}/{n} perSlot={spinDuration / n:F3}s");

        if (validIndices.Count == 0)
        {
            GameLog.Warning("[BossWheelPanel] SpinRoutine: no valid slots, skip");
            SkipAndShowResult();
            yield break;
        }

        ClearAllHighlights();
        float perSlot = spinDuration / n;
        int prevIdx = -1;

        // ── Phase 1：快转一圈 ──
        int startIdx = Random.Range(0, n);
        GameLog.Info($"[BossWheelPanel] Phase1: startIdx={startIdx}");

        for (int i = 0; i < n; i++)
        {
            int idx = (startIdx + i) % n;
            if (wheelSlots[idx] == null || !wheelSlots[idx].gameObject.activeInHierarchy)
                continue;

            // 关掉上一个高亮
            if (prevIdx >= 0 && prevIdx < n && wheelSlots[prevIdx] != null)
                wheelSlots[prevIdx].SetHighlight(false);

            // 打开当前高亮
            wheelSlots[idx].SetHighlight(true);
            prevIdx = idx;

            AudioService.Ensure().Play(AudioId.WheelTick);

            yield return new WaitForSecondsRealtime(perSlot);
        }

        GameLog.Info($"[BossWheelPanel] Phase1 done, Phase2: {_winningIndices.Length} winners");

        // ── Phase 2：依次停在每张中奖卡上，已中奖的高亮保持 ──
        var winnerSet = new System.Collections.Generic.HashSet<int>();
        for (int w = 0; w < _winningIndices.Length; w++)
        {
            int targetIdx = _winningIndices[w];
            GameLog.Info($"[BossWheelPanel] Phase2: winner[{w}] → slot[{targetIdx}] prevIdx={prevIdx}");

            if (targetIdx < 0 || targetIdx >= n || wheelSlots[targetIdx] == null)
            {
                GameLog.Warning($"[BossWheelPanel] Phase2: invalid targetIdx={targetIdx}");
                continue;
            }

            // 从当前位置快走到目标前（保留已中奖槽位的高亮）
            int totalSteps = extraLapsBeforeWinner * n + ((targetIdx - prevIdx - 1 + n * 2) % n);
            float quickStep = Mathf.Min(perSlot * 0.5f, 0.06f);
            GameLog.Info($"[BossWheelPanel] Phase2: extraLaps={extraLapsBeforeWinner} totalSteps={totalSteps}");

            for (int s = 0; s < totalSteps; s++)
            {
                int nextIdx = (prevIdx + 1) % n;
                while (wheelSlots[nextIdx] == null || !wheelSlots[nextIdx].gameObject.activeInHierarchy)
                    nextIdx = (nextIdx + 1) % n;

                // 只关非中奖的高亮
                if (prevIdx >= 0 && prevIdx < n && wheelSlots[prevIdx] != null && !winnerSet.Contains(prevIdx))
                    wheelSlots[prevIdx].SetHighlight(false);

                wheelSlots[nextIdx].SetHighlight(true);
                prevIdx = nextIdx;

                AudioService.Ensure().Play(AudioId.WheelTick);

                yield return new WaitForSecondsRealtime(quickStep);
            }

            // 最后一步落到目标（关掉当前非中奖高亮，开目标高亮并标记）
            if (prevIdx != targetIdx)
            {
                if (prevIdx >= 0 && prevIdx < n && wheelSlots[prevIdx] != null && !winnerSet.Contains(prevIdx))
                    wheelSlots[prevIdx].SetHighlight(false);

                wheelSlots[targetIdx].SetHighlight(true);
                prevIdx = targetIdx;
                yield return new WaitForSecondsRealtime(0.2f);
            }

            // 标记中奖，高亮保持
            winnerSet.Add(targetIdx);
            AudioService.Ensure().Play(AudioId.WheelTick);
            GameLog.Info($"[BossWheelPanel] Phase2: winner[{w}] at slot[{targetIdx}] locked, pausing {stopDuration}s");
            yield return new WaitForSecondsRealtime(stopDuration);
        }

        GameLog.Info("[BossWheelPanel] SpinRoutine done");
        ShowResult();
    }

    #endregion

    #region 结算

    private void ShowResult()
    {
        if (_resultShown)
        {
            GameLog.Warning("[BossWheelPanel] ShowResult: already shown");
            return;
        }
        _resultShown = true;

        GameLog.Info("[BossWheelPanel] ShowResult: invoking OnSpinComplete");
        _onSpinComplete?.Invoke();

        // 填充奖励 Cell（同技能多次中奖时等级累加显示）
        if (rewardCells != null)
        {
            var levelBonus = new System.Collections.Generic.Dictionary<SkillId, int>();
            for (int i = 0; i < rewardCells.Length; i++)
            {
                if (rewardCells[i] == null) continue;

                if (i < _winningIndices.Length)
                {
                    int idx = _winningIndices[i];
                    if (idx >= 0 && idx < _slots.Length)
                    {
                        var src = _slots[idx];
                        int bonus = levelBonus.TryGetValue(src.skillId, out int b) ? b : 0;
                        int displayCurrent = src.currentLevel + bonus;
                        levelBonus[src.skillId] = bonus + 1;

                        // 构造展示用副本（等级已累加）
                        var displayData = new WheelSlotData
                        {
                            skillId = src.skillId,
                            def = src.def,
                            currentLevel = displayCurrent,
                            targetLevel = displayCurrent + 1,
                            isActive = src.isActive,
                            weight = src.weight,
                        };
                        rewardCells[i].Bind(displayData);
                    }
                    else
                    {
                        rewardCells[i].gameObject.SetActive(false);
                    }
                }
                else
                {
                    rewardCells[i].gameObject.SetActive(false);
                }
            }
        }

        if (resultGroup != null)
            resultGroup.SetActive(true);

        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(true);
            confirmButton.interactable = true;
        }
    }

    #endregion

    private void OnDestroy()
    {
        GameLog.Info("[BossWheelPanel] OnDestroy");
        if (spinButton != null)
            spinButton.onClick.RemoveListener(OnSpinClicked);
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (skipAnimationToggle != null)
            skipAnimationToggle.onValueChanged.RemoveListener(OnSkipToggleChanged);
    }
}
