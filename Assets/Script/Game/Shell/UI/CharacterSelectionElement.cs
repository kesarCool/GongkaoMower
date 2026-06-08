using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选角列表子控件：头像 / 名字 / 选中高亮边框 / 解锁状态。
/// 挂在 CharacterSelectionPanel Prefab 的 Cell 上，Prefab 里需有对应序列化引用。
/// </summary>
[DisallowMultipleComponent]
public class CharacterSelectionElement : MonoBehaviour
{
    [Header("UI 子控件（Inspector 拖拽）")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI subText;
    [SerializeField] private Image highlightBorder;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private GameObject selectedCheckmark;
    [SerializeField] private Button clickButton;

    public CharacterDefinition CharacterDef { get; private set; }
    public int Index { get; private set; }

    private System.Action<CharacterSelectionElement> _onClick;

    private void Reset()
    {
        if (avatarImage == null) avatarImage = transform.Find("Avatar")?.GetComponent<Image>();
        if (nameText == null) nameText = transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
        if (highlightBorder == null) highlightBorder = GetComponent<Image>();
        if (clickButton == null) clickButton = GetComponent<Button>();
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

    public void Bind(CharacterDefinition def, int index, bool isSelected, System.Action<CharacterSelectionElement> onClick)
    {
        CharacterDef = def;
        Index = index;
        _onClick = onClick;

        // 头像
        if (avatarImage != null && def.portrait != null)
        {
            avatarImage.sprite = def.portrait;
            avatarImage.enabled = true;
        }
        else if (avatarImage != null)
        {
            avatarImage.enabled = false;
        }

        // 名字
        if (nameText != null)
        {
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(nameText);
            nameText.text = def.displayName;
        }

        // 副标题（武器名 / 技能名）
        if (subText != null)
        {
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(subText);
            if (def.defaultWeapon != null)
                subText.text = def.defaultWeapon.displayName;
            else
                subText.text = def.startingSkill != SkillId.None ? $"技能：{def.startingSkill}" : "";
        }

        // 锁定状态
        bool unlocked = CharacterUnlockEvaluator.IsUnlocked(def);
        if (lockedOverlay != null)
            lockedOverlay.SetActive(!unlocked);
        // 按钮保持可点击，未解锁时点击弹出提示
        if (clickButton != null)
            clickButton.interactable = true;

        // 选中高亮
        SetSelected(isSelected);
    }

    public void SetSelected(bool selected)
    {
        if (highlightBorder != null)
            highlightBorder.color = selected
                ? new Color(1f, 0.82f, 0.15f, 1f)
                : new Color(0.3f, 0.3f, 0.35f, 1f);

        if (selectedCheckmark != null)
            selectedCheckmark.SetActive(selected);
    }

    private void OnClicked()
    {
        UiClickSound.Play();
        _onClick?.Invoke(this);
    }
}
