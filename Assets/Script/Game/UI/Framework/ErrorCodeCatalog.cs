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

        _byCode = new Dictionary<string, ErrorCodeTable>(StringComparer.OrdinalIgnoreCase);
        if (TableManager.Instance == null)
            return;

        var dict = TableManager.Instance.GetTable<ErrorCodeTable>();
        if (dict == null)
            return;

        foreach (var kv in dict)
        {
            if (!(kv.Value is ErrorCodeTable entry))
                continue;
            if (string.IsNullOrEmpty(entry.ErrorCode))
                continue;
            _byCode[entry.ErrorCode] = entry;
        }
    }
#else
    public static bool TryGet(string errorCode, out object row)
    {
        row = null;
        return false;
    }
#endif
}
