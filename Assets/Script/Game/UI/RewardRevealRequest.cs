using System;
using System.Collections.Generic;

/// <summary>A read-only presentation receipt for rewards already granted and saved by a business service.</summary>
[Serializable]
public sealed class RewardRevealRequest
{
    public string receiptId, source, title, characterId;
    public List<RewardItemEntry> items = new List<RewardItemEntry>();

    public static RewardItemEntry Item(int itemId, int count)
    {
        var row = TableManager.Instance.GetTableItem<ProtoTable.ItemTable>(itemId) as ProtoTable.ItemTable;
        return new RewardItemEntry
        {
            itemId = itemId, count = count, itemName = row != null ? row.ItemName : itemId == 1 ? "金币" : itemId == 2 ? "钻石" : "物品",
            iconPath = row != null ? row.IconPath : "", grade = row != null ? row.Grade : 0,
            description = row != null ? row.Description : ""
        };
    }

    public static RewardItemEntry Copy(RewardItemEntry item)
    {
        return new RewardItemEntry { itemId = item.itemId, itemName = item.itemName, iconPath = item.iconPath,
            count = item.count, grade = item.grade, description = item.description };
    }
}
