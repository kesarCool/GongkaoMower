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

    private IEnumerator Start()
    {
        yield return null; // 等一帧，UIManager & GameLayer 就位

        if (progression == null && !string.IsNullOrEmpty(progressionResourcePath))
            progression = Resources.Load<CardPoolProgression>(progressionResourcePath);
        if (progression == null)
        {
            Debug.LogWarning("[GameUnlockSkill] 未找到 CardPoolProgression，跳过。");
            yield break;
        }

        int currentLevel = BattleLevelContext.LevelId;
        if (currentLevel <= 0)
        {
            Debug.Log("[GameUnlockSkill] 关卡 ID 无效，跳过。");
            yield break;
        }

        // 收集本关解锁的技能
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

        if (unseen.Count == 0)
        {
            Debug.Log($"[GameUnlockSkill] 关卡 {currentLevel} 没有待展示的新技能。");
            yield break;
        }

        // 逐个弹出
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
        }

        PlayerPrefs.Save();
    }

    private SkillDefinitionBase FindSkillDef(SkillId id)
    {
        if (skillCatalog != null)
            return skillCatalog.Get(id);

        skillCatalog = FindObjectOfType<SkillCatalog>();
        if (skillCatalog != null)
            return skillCatalog.Get(id);

        skillCatalog = Resources.Load<SkillCatalog>("SkillCatalog");
        return skillCatalog?.Get(id);
    }
}
