using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/UI/Backpack Categories", fileName = "BackpackCategories")]
public sealed class BackpackCategoryCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Category
    {
        public string id, label, heading;
        public Sprite icon;
        public bool allItems;
        public int[] itemTypes;
        public bool Matches(int type) => allItems || (itemTypes != null && Array.IndexOf(itemTypes, type) >= 0);
    }
    public Category[] categories = Array.Empty<Category>();
}
