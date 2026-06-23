using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单条成就行（显示在 AchievementPanel 的列表中）。
/// 显示：描述文本、进度条（Image.fillAmount）、奖励 ItemCell、领取按钮。
/// </summary>
public class AchievementCell : MonoBehaviour
{
    private static readonly Color32 ColorNormal = new Color32(0x15, 0x4A, 0x7D, 0xFF);
    private static readonly Color32 ColorClaimed = new Color32(0x9D, 0x9D, 0x9D, 0xFF);

    [Header("背景")]
    [SerializeField] private Image bgImage;

    [Header("描述")]
    [SerializeField] private TMPro.TextMeshProUGUI textDescription;
    [SerializeField] private TMPro.TextMeshProUGUI textProgress;     // "150/1000"

    [Header("进度条（Image.fillAmount）")]
    [SerializeField] private Image progressFill;

    [Header("奖励")]
    [SerializeField] private ItemCell rewardItemCell;

    [Header("状态标签")]
    [SerializeField] private Button btnClaim;          // 可领取时显示
    [SerializeField] private GameObject objClaimed;    // 已领取 ✓ 标记
    [SerializeField] private GameObject objNotAchieved; // 未达成标签

    private int _groupId;
    private int _stage;
    private System.Action<int, int> _onClaimClicked; // (groupId, stage)

    public void Bind(AchievementService.StageInfo stageInfo, AchievementService.Group group,
        System.Action<int, int> onClaimClicked)
    {
        _groupId = group.groupId;
        _stage = stageInfo.stage;
        _onClaimClicked = onClaimClicked;

        bool locked = !stageInfo.isUnlocked;
        bool claimed = stageInfo.isClaimed;
        bool canClaim = stageInfo.isUnlocked && stageInfo.isCompleted && !stageInfo.isClaimed;

        // 背景色
        if (bgImage != null)
            bgImage.color = claimed ? ColorClaimed : ColorNormal;

        // 描述
        if (textDescription != null)
        {
            textDescription.text = locked ? "???" : string.Format(stageInfo.description, stageInfo.targetValue);
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(textDescription);
            textDescription.gameObject.SetActive(true);
        }

        // 进度文本 & 进度条
        if (locked)
        {
            if (textProgress != null) textProgress.gameObject.SetActive(false);
            if (progressFill != null) progressFill.gameObject.SetActive(false);
        }
        else
        {
            int current = Mathf.Min(group.currentValue, stageInfo.targetValue);
            if (textProgress != null)
            {
                textProgress.text = $"{current}/{stageInfo.targetValue}";
                BattleChineseFontRuntime.EnsureLoaded();
                BattleChineseFontRuntime.ApplyToTMP(textProgress);
                textProgress.gameObject.SetActive(true);
            }
            if (progressFill != null)
            {
                progressFill.fillAmount = stageInfo.targetValue > 0
                    ? (float)current / stageInfo.targetValue
                    : 0f;
                progressFill.gameObject.SetActive(true);
            }
        }

        // 奖励 ItemCell
        if (rewardItemCell != null)
        {
            if (locked)
            {
                rewardItemCell.gameObject.SetActive(false);
            }
            else
            {
                rewardItemCell.gameObject.SetActive(true);
                BindRewardCell(stageInfo.rewardId, stageInfo.rewardCount);
            }
        }

        // 状态标签：已领取 / 可领取 / 未达成
        if (objClaimed != null)
            objClaimed.SetActive(claimed);

        if (btnClaim != null)
            btnClaim.gameObject.SetActive(canClaim && !claimed);

        if (objNotAchieved != null)
            objNotAchieved.SetActive(!locked && !claimed && !canClaim);
    }

    private void OnEnable()
    {
        if (btnClaim != null)
            btnClaim.onClick.AddListener(OnClaimClicked);
    }

    private void OnDisable()
    {
        if (btnClaim != null)
            btnClaim.onClick.RemoveListener(OnClaimClicked);
    }

    private void OnClaimClicked()
    {
        UiClickSound.Play();
        _onClaimClicked?.Invoke(_groupId, _stage);
    }

    /// <summary>从 ItemTable 查询物品信息并绑定到 ItemCell。</summary>
    private void BindRewardCell(int rewardId, int rewardCount)
    {
        string itemName = "";
        Sprite icon = null;
        int grade = 0;

#if USE_FB_TABLE
        var dict = TableManager.Instance?.GetTable<ProtoTable.ItemTable>();
        if (dict != null)
        {
            foreach (var kv in dict)
            {
                if (kv.Value is ProtoTable.ItemTable row && row.ID == rewardId)
                {
                    var rowType = row.GetType();
                    string nameVal = null;
                    string[] nameCandidates = { "Name", "ItemName", "DisplayName", "CNName", "name" };
                    foreach (var n in nameCandidates)
                    {
                        var p = rowType.GetProperty(n);
                        if (p != null) { nameVal = p.GetValue(row)?.ToString(); break; }
                        var f = rowType.GetField(n);
                        if (f != null) { nameVal = f.GetValue(row)?.ToString(); break; }
                    }
                    itemName = nameVal ?? "";

                    var gradeProp = rowType.GetProperty("Grade");
                    if (gradeProp != null && gradeProp.GetValue(row) is int g) grade = g;
                    else
                    {
                        var gradeField = rowType.GetField("Grade");
                        if (gradeField != null && gradeField.GetValue(row) is int gf) grade = gf;
                    }

                    string iconPath = null;
                    var iconProp = rowType.GetProperty("IconPath");
                    if (iconProp != null) iconPath = iconProp.GetValue(row)?.ToString();
                    else
                    {
                        var iconField = rowType.GetField("IconPath");
                        if (iconField != null) iconPath = iconField.GetValue(row)?.ToString();
                    }
                    if (!string.IsNullOrEmpty(iconPath))
                        icon = Resources.Load<Sprite>(iconPath);
                    break;
                }
            }
        }
#endif

        if (icon == null)
            icon = Resources.Load<Sprite>("UI/Items/icon_diamond");
        if (string.IsNullOrEmpty(itemName))
            itemName = "钻石";

        rewardItemCell.Bind(icon, itemName, rewardCount, grade);
    }

#if UNITY_EDITOR
    [ContextMenu("自动绑定子控件")]
    private void Reset()
    {
        bgImage = GetComponent<Image>();
        textDescription = transform.Find("TextDescription")?.GetComponent<TMPro.TextMeshProUGUI>();
        textProgress = transform.Find("TextProgress")?.GetComponent<TMPro.TextMeshProUGUI>();
        progressFill = transform.Find("ProgressFill")?.GetComponent<Image>();
        rewardItemCell = transform.Find("RewardItemCell")?.GetComponent<ItemCell>();
        btnClaim = transform.Find("BtnClaim")?.GetComponent<Button>();
        objClaimed = transform.Find("ObjClaimed")?.gameObject;
        objNotAchieved = transform.Find("ObjNotAchieved")?.gameObject;
    }
#endif
}
