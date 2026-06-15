using System.Collections.Generic;
using ProtoTable;
using UnityEngine;

/// <summary>
/// ShopTable 缓存查询：按 ShopType 分类 + ID 直查。
/// TableManager.Init() 后调用 InvalidateCache() 刷新。
/// </summary>
public class ShopCatalog
{
    private static ShopCatalog _instance;
    public static ShopCatalog Instance => _instance ??= new ShopCatalog();

    private readonly List<ShopTable> _normalItems = new List<ShopTable>(); // ShopType=normal
    private readonly Dictionary<int, ShopTable> _byId = new Dictionary<int, ShopTable>();
    private bool _loaded;

    public IReadOnlyList<ShopTable> NormalItems
    {
        get { EnsureLoaded(); return _normalItems; }
    }

    public ShopTable Get(int id)
    {
        EnsureLoaded();
        _byId.TryGetValue(id, out var row);
        return row;
    }

    public void InvalidateCache()
    {
        _normalItems.Clear();
        _byId.Clear();
        _loaded = false;
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

#if USE_FB_TABLE
        var dict = TableManager.Instance?.GetTable<ShopTable>();
        if (dict == null) return;

        foreach (var kv in dict)
        {
            if (!(kv.Value is ShopTable row)) continue;
            _byId[row.ID] = row;

            if (row.ShopType == ShopTable.eShopType.normal)
                _normalItems.Add(row);
        }

        // 按 ID 排序保证稳定的展示顺序
        _normalItems.Sort((a, b) => a.ID.CompareTo(b.ID));
#endif
    }
}
