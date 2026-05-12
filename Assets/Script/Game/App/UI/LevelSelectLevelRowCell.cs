using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡行 Cell：展示关卡名/ID，点击写入 <see cref="SelectedLevelContext"/>。
/// </summary>
[DisallowMultipleComponent]
public class LevelSelectLevelRowCell : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Button clickButton;

    private LevelSelectFlatRow _row;

    private void Reset()
    {
        if (titleText == null)
            titleText = GetComponentInChildren<Text>(true);
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

        if (titleText != null)
        {
            if (!string.IsNullOrEmpty(row.mapName))
                titleText.text = row.mapName;
            else
                titleText.text = $"关卡 {row.levelId}";
        }
    }

    private void OnClicked()
    {
        if (_row.Kind != LevelSelectRowKind.Level)
            return;
        SelectedLevelContext.Set(_row.chapterId, _row.levelId);
    }
}
