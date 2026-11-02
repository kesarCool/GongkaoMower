using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation bindings only. Battle outcomes, reward grants and ad callbacks remain in their owners.</summary>
public sealed class BattlePanelPresentation : MonoBehaviour
{
    public TMP_Text level, wave, duration, kills, activeCount, passiveCount, subtitle, title;
    public Image outcomeIcon;
    public Sprite victoryIcon, defeatIcon;
    public GameObject stars;
    public ScrollRect activeScroll, rewardScroll;
    public Transform activeContent, passiveContent;
    public GameObject passivePrefab;
    public TMP_Text emptyActive, emptyReward;
    public void Present(GameResultViewModel result = null)
    {
        WarmFont();
        var hud = FindObjectOfType<GameLayer>();
        if (level != null) level.text = BattleLevelContext.GetDisplayText();
        if (wave != null) wave.text = result != null ? result.completedWaves + " / " + result.totalWaves : hud != null ? hud.CurrentWave + " / " + hud.TotalWaves : "—";
        if (kills != null) kills.text = (result != null ? result.killCount : hud != null ? hud.CurrentKills : 0).ToString();
        if (duration != null) { int s = Mathf.FloorToInt(result != null ? result.battleDurationUnscaled : hud != null ? hud.ElapsedSeconds : 0); duration.text = (s / 60).ToString("00") + ":" + (s % 60).ToString("00"); }
        var skills = FindObjectOfType<PlayerSkills>(); var ids = new List<SkillId>();
        if (skills != null) skills.GetEquippedSkillIdsOrdered(ids);
        if (activeCount != null) activeCount.text = ids.Count + " / " + PlayerSkills.MAX_SKILL_SLOTS;
        if (emptyActive != null) emptyActive.gameObject.SetActive(ids.Count == 0);
        float total = 0; foreach (var id in ids) total += BattleRunMetrics.GetSkillDamage(id);
        if (activeContent != null)
        {
            var rows = activeContent.GetComponentsInChildren<GameResultSkillDamageCell>();
            for (int i = 0; i < rows.Length && i < ids.Count; i++) rows[i].SetDamageShare(total > 0 ? BattleRunMetrics.GetSkillDamage(ids[i]) / total : 0, i);
        }
        ids.Clear(); if (skills != null) skills.GetEquippedPassiveIdsOrdered(ids);
        if (passiveCount != null) passiveCount.text = ids.Count + " / " + PlayerSkills.MAX_PASSIVE_SLOTS;
        if (passiveContent != null && passivePrefab != null)
            for (int i = ids.Count; i < PlayerSkills.MAX_PASSIVE_SLOTS; i++)
                Instantiate(passivePrefab, passiveContent, false).GetComponent<GamePassiveSkillCell>().Bind(null, "未装备", 0);
        if (activeScroll != null) { Canvas.ForceUpdateCanvases(); activeScroll.StopMovement(); activeScroll.verticalNormalizedPosition = 1; }
        if (rewardScroll != null) { rewardScroll.StopMovement(); rewardScroll.horizontalNormalizedPosition = 0; }
        if (result != null)
        {
            if (title != null) { title.text = result.victory ? "战斗胜利" : "战斗失败"; title.color = result.victory ? new Color32(181, 129, 42, 255) : new Color32(118, 99, 76, 255); }
            if (outcomeIcon != null) outcomeIcon.sprite = result.victory ? victoryIcon : defeatIcon;
            if (stars != null) stars.SetActive(result.victory);
            if (subtitle != null) subtitle.gameObject.SetActive(!result.victory);
            if (emptyReward != null) emptyReward.gameObject.SetActive(result.rewardItems == null || result.rewardItems.Count == 0);
        }
        var layout = GetComponent<BattleThemeLayout>(); if (layout != null) layout.Refresh();
    }
    public static void Clear(Transform content)
    {
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--) { var child = content.GetChild(i); child.gameObject.SetActive(false); child.SetParent(null, false); Destroy(child.gameObject); }
    }
    public static void WarmFont()
    {
        BattleChineseFontRuntime.EnsureLoaded(); var font = BattleChineseFontRuntime.LoadedFont;
        if (font != null && font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            font.TryAddCharacters("战斗胜利失败调整搭配再来一局输出技能被动未装备获得奖励暂无音乐背景效继续退出关卡返回主界面下一试次累计伤害完成波本时长击杀数量满血复活当前倒计秒结束将进入结算看广告新角色解锁前往查看能量首领", out _);
    }
}
