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

    private LevelSelectFlatRow _row;

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

        if (titleText != null)
        {
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(titleText);

            string name;
            if (!string.IsNullOrEmpty(row.mapName))
                name = row.mapName;
            else
                name = $"关卡 {row.levelId}";

            if (!unlocked)
                name += "（未解锁）";
            else if (PlayerProfileService.Instance.TryGetProgress(row.levelId, out var prog) && prog.cleared)
            {
                int earned = Mathf.Clamp(prog.stars, 0, 3);
                int unearned = 3 - earned;
                if (earned > 0 || unearned > 0)
                    name += " " + new string('★', earned) + new string('☆', unearned);
            }

            titleText.text = name;
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
        return $"是否进入「{ChapterLevelDisplay.FormatLevelName(_row.levelId, _row.mapName)}」？";
    }
}
