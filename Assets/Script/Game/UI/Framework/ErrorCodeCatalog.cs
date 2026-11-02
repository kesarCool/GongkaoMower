using System;
using System.Collections.Generic;
using UnityEngine;

#if USE_FB_TABLE
using ProtoTable;
#endif

/// <summary>从 <c>ErrorCodeTable</c> 配表按 <see cref="ErrorCodeTable.ErrorCode"/> 查询展示配置。</summary>
public static class ErrorCodeCatalog
{
    public const int DisplayNone = 0;
    public const int DisplayToast = 1;
    public const int DisplayDialog = 2;

#if USE_FB_TABLE
    private static Dictionary<string, ErrorCodeTable> _byCode;
#endif

    public static void InvalidateCache()
    {
#if USE_FB_TABLE
        _byCode = null;
#endif
    }

#if USE_FB_TABLE
    public static bool TryGet(string errorCode, out ErrorCodeTable row)
    {
        row = null;
        if (string.IsNullOrEmpty(errorCode))
            return false;

        EnsureCache();
        return _byCode != null && _byCode.TryGetValue(errorCode, out row);
    }

    private static void EnsureCache()
    {
        if (_byCode != null)
            return;

        // 表未就绪时不得发布空缓存（否则整个会话错误码查询失效且无自愈路径）：
        // GetTable 缺表时返回空字典而非 null，须用 Count 判定；留空让下次 TryGet 重试。
        if (TableManager.Instance == null)
            return;

        var dict = TableManager.Instance.GetTable<ErrorCodeTable>();
        if (dict == null || dict.Count == 0)
            return;

        var cache = new Dictionary<string, ErrorCodeTable>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in dict)
        {
            if (!(kv.Value is ErrorCodeTable entry))
                continue;
            if (string.IsNullOrEmpty(entry.ErrorCode))
                continue;
            cache[entry.ErrorCode] = entry;
        }
        _byCode = cache;
    }
#else
    public static bool TryGet(string errorCode, out object row)
    {
        row = null;
        return false;
    }
#endif
}
