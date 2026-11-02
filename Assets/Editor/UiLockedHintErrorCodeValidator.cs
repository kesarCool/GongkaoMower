using System;
using System.Collections.Generic;
using System.Text;
using ProtoTable;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 锁定提示错误码校验（菜单 Tools/UI/校验锁定提示错误码）：
/// 1) 六个新增码 + 三个复用码是否已在导出表正确配置（CodeText 非空、CodeDisplay=1）；
/// 2) 扫描所有 Prefab 上 UiLockedHint 的固定 errorCode 是否存在于表中（动态 Provider 无法静态校验，跳过）。
/// 数据未就绪（找不到 bytes / 解析失败）时报告"无法校验"，不误报"错误码不存在"（任务书 §2.3）。
/// 不经运行时 TableManager/ErrorCodeCatalog，读法同 TableManager._fillTableFromFlatBufferBytes。
/// </summary>
public static class UiLockedHintErrorCodeValidator
{
    private const string TableAssetPath = "Assets/Resources/Data/table_fb/ErrorCodeTable.bytes";

    // [错误码, 期望文案关键字（空=只查存在）, 期望 CodeDisplay（空=不校验）]
    private static readonly string[,] RequiredCodes =
    {
        { "ERR_GOLD_NOT_ENOUGH", "金币", "1" },
        { "ERR_FRAGMENT_NOT_ENOUGH", "碎片", "1" },
        { "ERR_STAGE_LEVEL_NOT_ENOUGH", "等级", "1" },
        { "ERR_SHOP_SOLD_OUT", "售罄", "1" },
        { "ERR_GIFT_CODE_ALREADY_CLAIMED", "兑换", "1" },
        { "ERR_LEVEL_NEXT_LOCKED", "解锁", "1" },
        { "ERR_LEVEL_NO_CONTEXT", "", "" },
        { "ERR_LEVEL_NO_NEXT", "", "" },
        { "ERR_LEVEL_LOCKED", "", "" },
    };

    [MenuItem("Tools/UI/校验锁定提示错误码")]
    public static void Validate()
    {
        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(TableAssetPath);
        if (textAsset == null || textAsset.bytes == null || textAsset.bytes.Length == 0)
        {
            Debug.LogWarning($"[UiLockedHint校验] 无法校验：找不到导出数据 {TableAssetPath}，请先跑导表再执行。");
            return;
        }

        Dictionary<string, ProtoTable.ErrorCodeTable> rows;
        try
        {
            rows = ParseTable(textAsset.bytes);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UiLockedHint校验] 无法校验：解析失败（{e.GetType().Name}: {e.Message}），请确认导表产物与 ProtoTable 定义同版本。");
            return;
        }

        var report = new StringBuilder();
        int errors = 0;

        for (int i = 0; i < RequiredCodes.GetLength(0); i++)
        {
            string code = RequiredCodes[i, 0];
            string expectText = RequiredCodes[i, 1];
            string expectDisplay = RequiredCodes[i, 2];

            if (!rows.TryGetValue(code, out var row))
            {
                report.AppendLine($"[缺失] 错误码未在表中：{code}");
                errors++;
                continue;
            }
            if (string.IsNullOrEmpty(row.CodeText))
            {
                report.AppendLine($"[配置错] {code}：CodeText 为空");
                errors++;
            }
            else if (expectText.Length > 0 && !row.CodeText.Contains(expectText))
            {
                report.AppendLine($"[提醒] {code}：文案「{row.CodeText}」不含关键字「{expectText}」，请人工确认");
            }
            if (expectDisplay.Length > 0 && row.CodeDisplay.ToString() != expectDisplay)
            {
                report.AppendLine($"[配置错] {code}：CodeDisplay={row.CodeDisplay}，期望 {expectDisplay}");
                errors++;
            }
        }

        errors += ValidatePrefabCodes(rows, report);

        if (errors > 0)
        {
            report.Insert(0, $"[UiLockedHint校验] 表共 {rows.Count} 行错误码；发现 {errors} 处问题：\n");
            Debug.LogWarning(report.ToString());
        }
        else
        {
            Debug.Log($"[UiLockedHint校验] 表共 {rows.Count} 行错误码；新增/复用错误码与 Prefab 配置均通过。\n{report}");
        }
    }

    /// <summary>扫描 Prefab 上 UiLockedHint 的固定 errorCode，校验存在于表中；未挂载时提示而非报错。</summary>
    private static int ValidatePrefabCodes(Dictionary<string, ProtoTable.ErrorCodeTable> rows, StringBuilder report)
    {
        int errors = 0, mounted = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            foreach (var hint in go.GetComponentsInChildren<UiLockedHint>(true))
            {
                mounted++;
                if (string.IsNullOrEmpty(hint.FixedErrorCode)) continue;
                if (!rows.ContainsKey(hint.FixedErrorCode))
                {
                    report.AppendLine($"[配置错] Prefab 固定码不存在：{path} → {hint.FixedErrorCode}");
                    errors++;
                }
            }
        }
        report.AppendLine(mounted == 0
            ? "[提醒] 当前没有 Prefab 挂载 UiLockedHint（挂载后请再次执行本校验）"
            : $"[信息] 已扫描 {mounted} 个 UiLockedHint 挂载");
        return errors;
    }

    /// <summary>直接解析 flatbuffer 导表产物，不经运行时 TableManager（避免污染 ErrorCodeCatalog 缓存）。</summary>
    private static Dictionary<string, ProtoTable.ErrorCodeTable> ParseTable(byte[] data)
    {
        var result = new Dictionary<string, ProtoTable.ErrorCodeTable>();
        var ftable = new FlatBuffers.Table();
        var buffer = new FlatBuffers.ByteBuffer(data);
        ftable.bb_pos = 0;
        ftable.bb = buffer;

        int length = ftable.__vector_len(0);
        int vec0 = ftable.__vector(0);
        for (int index = 0; index < length; ++index)
        {
            int rowPos = ftable.__indirect(vec0 + index * 4);
            var row = (ProtoTable.ErrorCodeTable)Activator.CreateInstance(typeof(ProtoTable.ErrorCodeTable));
            row.__init(rowPos, ftable.bb);
            if (!string.IsNullOrEmpty(row.ErrorCode) && !result.ContainsKey(row.ErrorCode))
                result.Add(row.ErrorCode, row);
        }
        return result;
    }
}
