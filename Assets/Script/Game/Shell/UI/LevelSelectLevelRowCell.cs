using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡行 Cell：展示「序号 + 关卡名/ID」；点击已解锁关卡弹出二次确认，确认后进入战斗。
/// </summary>
[DisallowMultipleComponent]
public class LevelSelectLevelRowCell : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button clickButton;

    [Header("背景")]
    [SerializeField] private Image background;
    [Tooltip("已解锁时背景颜色。")]
    [SerializeField] private Color unlockedColor = Color.white;
    [Tooltip("未解锁时背景颜色。")]
    [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("星级")]
    [SerializeField] private TextMeshProUGUI starText;
    [Header("Optional themed state")]
    [SerializeField] private Sprite availableSprite, clearedSprite, lockedSprite;
    [SerializeField] private Image stateIcon;
    [SerializeField] private Sprite flagIcon, checkIcon, lockIcon;
    [SerializeField] private TextMeshProUGUI stateText;

    private LevelSelectFlatRow _row;

    public int LevelId => _row.levelId;

    private void Reset()
    {
        if (titleText == null)
            titleText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (clickButton == null)
            clickButton = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (clickButton != null)
            clickButton.onClick.AddListener(OnClicked);
    }

    private void OnDisable()
    {
        if (clickButton != null)
            clickButton.onClick.RemoveListener(OnClicked);
    }

    public void Bind(in LevelSelectFlatRow row)
    {
        _row = row;
        if (row.Kind != LevelSelectRowKind.Level)
            return;

        PlayerProfileService.Instance.LoadOrCreate();
        bool unlocked = PlayerProfileService.Instance.IsLevelUnlocked(row.levelId);

        if (clickButton != null)
            clickButton.interactable = unlocked;

        if (background != null)
            background.color = unlocked ? unlockedColor : lockedColor;

        if (titleText != null)
        {
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(titleText);

            string name = ChapterLevelDisplay.FormatLevelName(row.levelId, row.stageName);

            if (!unlocked && stateText == null)
                name += "（未解锁）";

            titleText.text = name;
        }

        if (availableSprite != null && background != null)
        {
            bool cleared = unlocked && PlayerProfileService.Instance.TryGetProgress(row.levelId, out var progress) && progress.cleared;
            background.sprite = !unlocked ? lockedSprite : cleared ? clearedSprite : availableSprite;
            background.color = Color.white;
            if (stateIcon != null) stateIcon.sprite = !unlocked ? lockIcon : cleared ? checkIcon : flagIcon;
            if (stateText != null) stateText.text = !unlocked ? "未解锁" : cleared ? "已通关" : "可挑战";
        }

        if (starText != null)
        {
            if (unlocked && PlayerProfileService.Instance.TryGetProgress(row.levelId, out var prog) && prog.cleared)
            {
                int earned = Mathf.Clamp(prog.stars, 0, 3);
                starText.text = new string('★', earned) + new string('☆', 3 - earned);
            }
            else
            {
                starText.text = "";
            }
        }
    }

    private void OnClicked()
    {
        if (_row.Kind != LevelSelectRowKind.Level)
            return;

        UiClickSound.Play();

        PlayerProfileService.Instance.LoadOrCreate();
        if (!PlayerProfileService.Instance.IsLevelUnlocked(_row.levelId))
            return;

        string message = BuildConfirmMessage();
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowConfirm("进入关卡", message, OnEnterConfirmResult, UiOpenOptions.ModalDefault);
            return;
        }

        SelectedLevelContext.Set(_row.chapterId, _row.levelId);
        BattleFlowLauncher.TryStartBattleLoading();
    }

    private void OnEnterConfirmResult(bool confirmed)
    {
        if (!confirmed)
            return;

        SelectedLevelContext.Set(_row.chapterId, _row.levelId);
        BattleFlowLauncher.TryStartBattleLoading();
    }

    private string BuildConfirmMessage()
    {
        return $"是否进入「{ChapterLevelDisplay.FormatLevelName(_row.levelId, _row.stageName)}」？";
    }
}
