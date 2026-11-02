using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡进入时检测 MainCardPoolProgression 是否有新技能解锁。
/// 新技能介绍排在 Loading、新手引导、选卡和其它弹窗之后，确认后恢复。
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
    private UIManager alertOwner;
    private int alertVersion;

    private IEnumerator Start()
    {
        yield return null;

        if (progression == null && !string.IsNullOrEmpty(progressionResourcePath))
            progression = Resources.Load<CardPoolProgression>(progressionResourcePath);

        int currentLevel = BattleLevelContext.LevelId;
        if (currentLevel <= 0)
        {
            GameLog.Info("[GameUnlockSkill] 关卡 ID 无效，跳过。");
            yield break;
        }

        if (progression == null)
        {
            Debug.LogWarning("[GameUnlockSkill] 未找到 CardPoolProgression，跳过。");
            yield break;
        }

        // Starter skills are explained by TutorialDirector. Never show the retired entry popup.
        if (currentLevel == 101)
        {
            if (progression.unlockConditions != null)
                foreach (var condition in progression.unlockConditions)
                    if (condition != null && condition.unlockAtLevel == currentLevel) MarkSeen(currentLevel, condition.skill);
            PlayerPrefs.Save();
            yield break;
        }

        if (skillCatalog == null) skillCatalog = Resources.Load<SkillCatalog>("SkillCatalog");

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
            GameLog.Info($"[GameUnlockSkill] 关卡 {currentLevel} 没有待展示的新技能（家族过滤后）。");
            yield break;
        }

        foreach (SkillId id in unseen)
        {
            var def = FindSkillDef(id);
            string title = "技能解锁";
            string msg;
            if (def != null)
                msg = $"新技能 <b><size=+16><color=#FF8800>「{def.displayName}」</color></size></b>\n{def.description}";
            else
                msg = $"新技能 {id} 解锁！";

            bool done = false;
            while (!done)
            {
                while (!CanPresentUnlock()) yield return null;
                alertOwner = UIManager.Instance;
                alertOwner.ShowAlert(title, msg, () => done = true);
                alertVersion = alertOwner.ConfirmationVersion;
                if (!alertOwner.IsConfirmationActive(alertVersion))
                {
                    alertOwner = null;
                    yield break; // Missing presentation must never mark an unseen skill as read.
                }
                while (!done && alertOwner != null && alertOwner.IsConfirmationActive(alertVersion)) yield return null;
                alertOwner = null;
                // Card selection may replace a confirmation silently. Retry after it closes rather than
                // waiting forever for a lost callback or pretending the user acknowledged the message.
                yield return null;
            }
            MarkSeen(currentLevel, id);
            PlayerPrefs.Save();
        }
    }

    public static bool CanPresentUnlock()
    {
        var ui = UIManager.Instance;
        return !BattleLoadingSceneController.IsTransitionVisible && !TutorialDirector.IsBattleIntroductionPending &&
            !TutorialDirector.IsShowing && ui != null && ui.CanPresentQueuedFeedback && ui.Top == null;
    }

    private static void MarkSeen(int level, SkillId id)
    {
        PlayerPrefs.SetInt(PrefPrefix + level + "_" + (int)id, 1);
        string globalKey = GlobalUnlockKeyPrefix + (int)id;
        PlayerPrefs.SetInt(globalKey, Mathf.Max(level, PlayerPrefs.GetInt(globalKey, 0)));
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (alertOwner != null && alertOwner.IsConfirmationActive(alertVersion)) alertOwner.CloseConfirm();
        alertOwner = null;
    }

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
                    GameLog.Info($"[GameUnlockSkill] 过滤技能 {def.displayName}({def.id})：家族 {family} 已被 {allowedId} 占用");
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
