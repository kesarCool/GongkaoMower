using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 暂停面板：展示当前技能、技能伤害累计、返回/退出按钮。
/// </summary>
public class GamePausePanel : UIPanelBase
{
    [Header("技能列表")]
    public Transform skillListParent;
    public GameObject skillRowPrefab;

    [Header("按钮")]
    public Button resumeButton;
    public Button quitButton;

    [Header("数据源")]
    public PlayerSkills playerSkills;

    private PlayerSkills _playerSkills;

    public override void OnOpen(object payload)
    {
        _playerSkills = playerSkills != null ? playerSkills : FindObjectOfType<PlayerSkills>();
        BuildSkillList();

        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResume);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuit);
    }

    public override void OnClose()
    {
        if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResume);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuit);
    }

    private void BuildSkillList()
    {
        if (skillListParent == null || skillRowPrefab == null) return;

        // 清掉旧行
        for (int i = skillListParent.childCount - 1; i >= 0; i--)
            Destroy(skillListParent.GetChild(i).gameObject);

        if (_playerSkills == null) return;

        var ids = new List<SkillId>();
        _playerSkills.GetEquippedSkillIdsOrdered(ids);

        SkillCatalog catalog = _playerSkills.skillCatalog;
        if (catalog == null) catalog = FindObjectOfType<SkillCatalog>();
        if (catalog == null) catalog = Resources.Load<SkillCatalog>("SkillCatalog");

        foreach (SkillId id in ids)
        {
            var def = catalog != null ? catalog.Get(id) : null;
            int level = _playerSkills.GetSkillLevel(id);
            float dmg = BattleRunMetrics.GetSkillDamage(id);

            GameObject row = Instantiate(skillRowPrefab, skillListParent, false);

            // 图标
            var iconImg = row.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null && def != null && def.icon != null)
                iconImg.sprite = def.icon;

            // 名字 + 等级
            var nameTxt = row.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            if (nameTxt != null && def != null)
                nameTxt.text = $"{def.displayName} Lv.{level}";

            // 伤害
            var dmgTxt = row.transform.Find("Damage")?.GetComponent<TextMeshProUGUI>();
            if (dmgTxt != null)
                dmgTxt.text = Mathf.CeilToInt(dmg).ToString();
        }
    }

    private void OnResume()
    {
        UIManager.Instance.CloseTop();
    }

    private void OnQuit()
    {
        UIManager.Instance.ShowConfirm("退出游戏", "确定要退出当前关卡吗？\n进度将不会保存。", confirmed =>
        {
            if (confirmed)
            {
                UIManager.Instance.CloseAllStack();
                Time.timeScale = 1f;
                SceneManager.LoadScene("Home");
            }
        });
    }
}
