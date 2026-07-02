using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>
/// 出怪后根据 <see cref="Monster"/> + <see cref="LexiconTable"/> 为「文字怪」赋值展示文案。
/// 约定：<c>Monster.type == <see cref="MonsterTypeIds.Word"/></c> 为文字怪；词条按 <c>CategoryTag</c> 数组匹配（<c>monster.CategoryTag</c> 为 int[]，<c>lx.CategoryTag</c> 命中数组中任一即匹配），
/// <c>ThemePackId</c> 在怪物表大于 0 时再按词库 <c>ThemePackId</c> 过滤（无命中则退回仅 CategoryTag）。
/// </summary>
public static class MonsterTypeIds
{
    /// <summary>配表 Monster.type：文字怪</summary>
    public const int Word = 1;
}

public static class MonsterWordSpawnBinding
{
    public const string LogTag = "[WordMonster]";

    /// <summary>关闭后不再输出本模块调试日志。</summary>
    public static bool LogVerbose = false;

    private static void L(string msg)
    {
        if (LogVerbose)
            Debug.Log($"{LogTag} {msg}");
    }

    private static void W(string msg)
    {
        if (LogVerbose)
            Debug.LogWarning($"{LogTag} {msg}");
    }

    public static void TryApply(GameObject enemy, int spawnMonsterId)
    {
        if (enemy == null)
        {
            W("TryApply: enemy is null");
            return;
        }

        L($"TryApply enter enemy={enemy.name} spawnMonsterId={spawnMonsterId}");

#if USE_FB_TABLE
        if (TableManager.Instance == null)
        {
            W("TryApply: TableManager.Instance is null（表未初始化）");
            return;
        }

        var monsterDict = TableManager.Instance.GetTable<Monster>();
        L($"Monster 表行数={monsterDict?.Count ?? -1}");

        if (!TryFindMonster(spawnMonsterId, out Monster monster))
        {
            W($"未找到 Monster 行：spawnMonsterId={spawnMonsterId}（请核对 monsterId 或 ID 与表一致）");
            return;
        }

        string catTags = monster.CategoryTagLength > 0
            ? string.Join("|", Enumerable.Range(0, monster.CategoryTagLength).Select(j => monster.CategoryTag[j]))
            : "0";
        L($"命中 Monster ID={monster.ID} monsterId={monster.monsterId} type={monster.type}(需={MonsterTypeIds.Word}为文字怪) CategoryTag=[{catTags}] ThemePackId={monster.ThemePackId} name={monster.name}");

        string display;

        if (monster.type != MonsterTypeIds.Word)
        {
            // Boss / 非文字怪：名字直接用 Monster.name，不走词库随机
            display = monster.name;
            L($"非文字怪 type={monster.type}，直接取 Monster.name={display}");
        }
        else
        {
            var lexDict = TableManager.Instance.GetTable<LexiconTable>();
            L($"Lexicon 表行数={lexDict?.Count ?? -1}");

            display = PickLexiconDisplayText(monster, out int poolStrict, out int poolLoose, out int poolUsed);
            if (string.IsNullOrEmpty(display))
                display = monster.name;

            string dispShort = display.Length > 48 ? display.Substring(0, 48) + "..." : display;
            L($"词条 strict={poolStrict} loose={poolLoose} 选用池条目数={poolUsed} displayLen={display.Length} display={dispShort}");
        }

        if (string.IsNullOrEmpty(display))
        {
            W("display 文本为空，跳过 SetWord");
            return;
        }

        var label = enemy.GetComponent<EnemyWordLabel>();
        if (label == null)
            label = enemy.GetComponentInChildren<EnemyWordLabel>(true);
        if (label == null)
            W($"Prefab 上无 EnemyWordLabel（根或子物体），无法写 TMP。enemy={enemy.name}");
        else
        {
            L($"调用 EnemyWordLabel.SetWord，label 所在节点={label.gameObject.name}");
            label.SetWord(display);
        }

        var eb = enemy.GetComponent<EnemyBase>();
        if (eb != null)
        {
            eb.SetRuntimeDisplayName(display);
            L($"已 SetRuntimeDisplayName（EnemyBase）");
        }
        else
            L("无 EnemyBase，仅尝试写了 Label");
#endif
    }

#if USE_FB_TABLE
    private static bool TryFindMonster(int spawnMonsterId, out Monster found)
    {
        found = null;
        var dict = TableManager.Instance.GetTable<Monster>();
        if (dict == null || dict.Count == 0)
            return false;

        foreach (var kv in dict)
        {
            if (kv.Value is Monster m && (m.monsterId == spawnMonsterId || m.ID == spawnMonsterId))
            {
                found = m;
                return true;
            }
        }

        return false;
    }

    /// <summary>检查 <paramref name="monster"/> 的 CategoryTag 数组是否包含 <paramref name="tag"/>（数组为空或含 0 视为不筛选，全选）。</summary>
    private static bool MonsterHasCategoryTag(Monster monster, int tag)
    {
        for (int j = 0; j < monster.CategoryTagLength; j++)
        {
            if (monster.CategoryTag[j] == 0 || monster.CategoryTag[j] == tag)
                return true;
        }
        return false;
    }

    private static string PickLexiconDisplayText(Monster monster, out int strictCount, out int looseCount, out int poolUsedCount)
    {
        strictCount = 0;
        looseCount = 0;
        poolUsedCount = 0;

        var dict = TableManager.Instance.GetTable<LexiconTable>();
        if (dict == null || dict.Count == 0)
            return null;

        var strict = new List<LexiconTable>();
        var loose = new List<LexiconTable>();

        foreach (var kv in dict)
        {
            if (!(kv.Value is LexiconTable lx))
                continue;
            if (monster.CategoryTagLength > 0 && !MonsterHasCategoryTag(monster, lx.CategoryTag))
                continue;

            // 敏感词过滤
            if (!SensitiveWordFilter.Instance.IsLexiconAllowed(lx.ID, lx.DisplayText))
                continue;

            loose.Add(lx);
            if (monster.ThemePackId > 0 && lx.ThemePackId == monster.ThemePackId)
                strict.Add(lx);
        }

        strictCount = strict.Count;
        looseCount = loose.Count;

        var pool = strict.Count > 0 ? strict : loose;
        if (pool.Count == 0)
            return null;

        poolUsedCount = pool.Count;

        float sum = 0f;
        for (int i = 0; i < pool.Count; i++)
            sum += Mathf.Max(0.0001f, pool[i].Weight);

        float r = Random.Range(0f, sum);
        float acc = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            acc += Mathf.Max(0.0001f, pool[i].Weight);
            if (r <= acc)
                return pool[i].DisplayText;
        }

        return pool[pool.Count - 1].DisplayText;
    }
#endif
}
