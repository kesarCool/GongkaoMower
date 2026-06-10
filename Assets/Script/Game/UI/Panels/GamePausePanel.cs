using System.Collections.Generic;
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
    [Header("被动技能列表")]
    public Transform passiveSkillListParent;
    public GameObject passiveSkillRowPrefab;
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
        BuildPassiveSkillList();

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

        for (int i = skillListParent.childCount - 1; i >= 0; i--)
            Destroy(skillListParent.GetChild(i).gameObject);

        if (_playerSkills == null) return;

        var ids = new List<SkillId>();
        _playerSkills.GetEquippedSkillIdsOrdered(ids);

        SkillCatalog catalog = _playerSkills.skillCatalog;
        if (catalog == null) catalog = Resources.Load<SkillCatalog>("SkillCatalog");

        foreach (SkillId id in ids)
        {
            var def = catalog != null ? catalog.Get(id) : null;
            int level = _playerSkills.GetSkillLevel(id);
            int maxLv = def != null ? def.maxLevel : 5;
            bool isBreakthrough = level >= maxLv;
            float dmg = BattleRunMetrics.GetSkillDamage(id);

            GameObject row = Instantiate(skillRowPrefab, skillListParent, false);

            var cell = row.GetComponent<GameResultSkillDamageCell>();
            if (cell != null)
            {
                string name = def != null ? $"Lv.{level} {def.displayName}" : id.ToString();
                Sprite icon = def != null ? def.icon : null;
                cell.Bind(icon, name, dmg);
                cell.SetBreakthrough(isBreakthrough);
            }
        }
    }

    private void BuildPassiveSkillList()
    {
        if (passiveSkillListParent == null || passiveSkillRowPrefab == null) return;

        for (int i = passiveSkillListParent.childCount - 1; i >= 0; i--)
            Destroy(passiveSkillListParent.GetChild(i).gameObject);

        if (_playerSkills == null) return;

        var ids = new List<SkillId>();
        _playerSkills.GetEquippedPassiveIdsOrdered(ids);

        SkillCatalog catalog = _playerSkills.skillCatalog;
        if (catalog == null) catalog = Resources.Load<SkillCatalog>("SkillCatalog");

        foreach (SkillId id in ids)
        {
            var def = catalog != null ? catalog.Get(id) : null;
            int level = _playerSkills.GetPassiveSkillLevel(id);

            GameObject row = Instantiate(passiveSkillRowPrefab, passiveSkillListParent, false);

            var cell = row.GetComponent<GamePassiveSkillCell>();
            if (cell != null)
            {
                string name = def != null ? def.displayName : id.ToString();
                Sprite icon = def != null ? def.icon : null;
                cell.Bind(icon, name, level);
            }
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
