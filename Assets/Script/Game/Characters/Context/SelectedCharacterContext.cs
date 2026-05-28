/// <summary>
/// 选角上下文：Home 场景写入，BattleLoading/Game 场景读取。
/// 与 <see cref="SelectedLevelContext"/> 同模式——纯静态。
/// 持久化到 <see cref="PlayerProfileService"/> 存档。
/// </summary>
public static class SelectedCharacterContext
{
    /// <summary>本次会话手动选中的角色 ID（未选则为 null）。</summary>
    public static string CharacterId { get; private set; }

    public static bool HasSelection => !string.IsNullOrEmpty(CharacterId);

    /// <summary>
    /// 选角面板确认时调用：写入静态上下文 + 持久化到存档。
    /// </summary>
    public static void Set(string characterId)
    {
        CharacterId = characterId;
        PlayerProfileService.Instance.SetEquippedCharacter(characterId ?? string.Empty);
    }

    public static void Clear()
    {
        CharacterId = null;
    }

    /// <summary>
    /// 获取当前生效的角色 ID：
    /// 1. 本次会话已手动选择 → 用选择值
    /// 2. 有持久化存档 → 用存档值
    /// 3. 以上均无 → 回退到 Catalog 默认角色
    /// </summary>
    public static string GetEffective(CharacterCatalog catalog)
    {
        // 本次会话已选
        if (!string.IsNullOrEmpty(CharacterId))
            return CharacterId;

        // 持久化存档
        PlayerProfileService.Instance.LoadOrCreate();
        string saved = PlayerProfileService.Instance.EquippedCharacterId;
        if (!string.IsNullOrEmpty(saved))
        {
            // 验证存档中的角色仍存在于 catalog
            if (catalog != null && catalog.Get(saved) != null)
                return saved;
        }

        // 兜底
        var def = catalog != null ? catalog.GetDefault() : null;
        return def != null ? def.characterId : null;
    }
}
