using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡进入时检测 MainCardPoolProgression 是否有新技能解锁。
/// 首次进入该关卡时通过 UIManager.ShowAlert 弹出介绍，暂停游戏，确认后恢复。
/// 已看过的状态存 PlayerPrefs，重复进同一关不再弹。
/// </summary>
[DisallowMultipleComponent]
public class GameUnlockSkill : MonoBehaviour
{
    [Header("Progression")]
    public CardPoolProgression progression;
    [Tooltip("留空则从 Resources 加载 MainCardPoolProgression")]
    public string progressionResourcePath = "CardPoolProgression/MainCardPoolProgression";
    public SkillCatalog skillCatalog;

    private const string PrefPrefix = "unlock_skill_seen_";
    private const string GlobalUnlockKeyPrefix = "global_skill_unlock_";
    private const string TutorialSeenKey = "tutorial_seen";

    private IEnumerator Start()
    {
        yield return null;

        if (progression == null && !string.IsNullOrEmpty(progressionResourcePath))
            progression = Resources.Load<CardPoolProgression>(progressionResourcePath);

        int currentLevel = BattleLevelContext.LevelId;
        if (currentLevel <= 0)
        {
            Debug.Log("[GameUnlockSkill] 关卡 ID 无效，跳过。");
            yield break;
        }

        // ── 新手引导：首次进 101 弹，之后永不再弹 ──
        if (currentLevel == 101 && PlayerPrefs.GetInt(TutorialSeenKey, 0) == 0)
        {
            bool done = false;
            UIManager.Instance.ShowAlert("操作指引",
                "滑动屏幕操控角色移动\n"
                + "角色会自动攻击附近的字灵\n"
                + "击败字灵收集能量,能量攒满后\n可选择技能升级\n"
                + "祝你一路披荆斩棘!",
                () => done = true);

            yield return new WaitUntil(() => done);
            PlayerPrefs.SetInt(TutorialSeenKey, 1);
            PlayerPrefs.Save();

            // 101 的技能解锁不在新手关弹，直接标记已展示
            if (progression != null && progression.unlockConditions != null)
            {
                foreach (var cond in progression.unlockConditions)
                {
                    if (cond == null || cond.unlockAtLevel != currentLevel) continue;
                    string key = PrefPrefix + currentLevel + "_" + (int)cond.skill;
                    PlayerPrefs.SetInt(key, 1);
                    PlayerPrefs.SetInt(GlobalUnlockKeyPrefix + (int)cond.skill, currentLevel);
                }
                PlayerPrefs.Save();
            }

            yield break;
        }

        // ── 技能解锁弹窗（101 除外，上面已处理）──
        if (progression == null)
        {
            Debug.LogWarning("[GameUnlockSkill] 未找到 CardPoolProgression，跳过。");
            yield break;
        }

        var unseen = new List<SkillId>();
        if (progression.unlockConditions != null)
        {
            foreach (var cond in progression.unlockConditions)
            {
                if (cond == null) continue;
                if (cond.unlockAtLevel != currentLevel) continue;

                string key = PrefPrefix + currentLevel + "_" + (int)cond.skill;
                if (PlayerPrefs.GetInt(key, 0) == 0)
                    unseen.Add(cond.skill);
            }
        }

        // 只展示当前角色能用到的技能（家族过滤）
        FilterByEquippedCharacter(unseen);

        if (unseen.Count == 0)
        {
            Debug.Log($"[GameUnlockSkill] 关卡 {currentLevel} 没有待展示的新技能（家族过滤后）。");
            yield break;
        }

        foreach (SkillId id in unseen)
        {
            bool done = false;
            var def = FindSkillDef(id);
            string title = "技能解锁";
            string msg;
            if (def != null)
                msg = $"新技能 <b><size=+16><color=#FF8800>「{def.displayName}」</color></size></b>\n{def.description}";
            else
                msg = $"新技能 {id} 解锁！";

            UIManager.Instance.ShowAlert(title, msg, () => done = true);
            yield return new WaitUntil(() => done);

            string key = PrefPrefix + currentLevel + "_" + (int)id;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.SetInt(GlobalUnlockKeyPrefix + (int)id, currentLevel);
        }

        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/清除新手引导记录", false, 500)]
    private static void ClearTutorialPrefs()
    {
        PlayerPrefs.DeleteKey(TutorialSeenKey);
        PlayerPrefs.DeleteKey(PrefPrefix + "101_" + (int)SkillId.AutoProjectile);
        PlayerPrefs.DeleteKey(PrefPrefix + "101_" + (int)SkillId.OrbitingBlades);
        PlayerPrefs.DeleteKey(PrefPrefix + "101_" + (int)SkillId.ThrowGrenade);
        PlayerPrefs.Save();
        UnityEditor.EditorUtility.DisplayDialog("清除完成", "新手引导和 101 技能解锁记录已清除。", "确定");
    }
#endif

    /// <summary>移除当前角色不会出现在卡池的技能（如非本变体的 AutoProjectile）。</summary>
    private void FilterByEquippedCharacter(List<SkillId> unseen)
    {
        if (unseen.Count == 0 || skillCatalog == null) return;

        // 收集已装备技能的家族
        var ps = FindObjectOfType<PlayerSkills>();
        if (ps == null) return;

        var equippedFamilies = new Dictionary<SkillId, SkillId>();
        // PlayerSkills 在 GameUnlockSkill.Start 协程里可能还未装备技能，
        // 从 SelectedCharacterContext 拿角色主技能兜底
        string charId = SelectedCharacterContext.GetEffective(null);
        SkillId mainSkill = SkillId.None;
        if (!string.IsNullOrEmpty(charId))
        {
            var charCatalog = Resources.Load<CharacterCatalog>("Character/CharacterCatalog");
            if (charCatalog != null)
            {
                var charDef = charCatalog.Get(charId);
                if (charDef != null) mainSkill = charDef.startingSkill;
            }
        }

        // 从已装备技能 + 角色主技能收集家族
        var activeIds = new List<SkillId>(5);
        ps.GetEquippedSkillIdsOrdered(activeIds);
        if (mainSkill != SkillId.None && !activeIds.Contains(mainSkill))
            activeIds.Add(mainSkill);

        foreach (var id in activeIds)
        {
            var def = skillCatalog.Get(id);
            if (def != null && def.SkillFamily != id)
                equippedFamilies[def.SkillFamily] = id;
        }

        // 过滤：家族已被其他变体占用 → 排除
        for (int i = unseen.Count - 1; i >= 0; i--)
        {
            var def = skillCatalog.Get(unseen[i]);
            if (def == null) continue;

            SkillId family = def.SkillFamily;
            if (family != def.id || equippedFamilies.ContainsKey(family))
            {
                if (!equippedFamilies.TryGetValue(family, out SkillId allowedId) || def.id != allowedId)
                {
                    Debug.Log($"[GameUnlockSkill] 过滤技能 {def.displayName}({def.id})：家族 {family} 已被 {allowedId} 占用");
                    unseen.RemoveAt(i);
                }
            }
        }
    }

    private SkillDefinitionBase FindSkillDef(SkillId id)
    {
        if (skillCatalog != null)
            return skillCatalog.Get(id);

        skillCatalog = Resources.Load<SkillCatalog>("SkillCatalog");
        return skillCatalog?.Get(id);
    }
}
