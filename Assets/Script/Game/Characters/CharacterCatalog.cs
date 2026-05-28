using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色目录：列出全部可用的 <see cref="CharacterDefinition"/>。
/// 与 <see cref="SkillCatalog"/> 同模式——列表型查找 SO。
/// </summary>
[CreateAssetMenu(menuName = "Game/Character Catalog", fileName = "CharacterCatalog")]
public class CharacterCatalog : ScriptableObject
{
    [Tooltip("默认角色（选角未选或首次启动时的兜底角色）")]
    public CharacterDefinition defaultCharacter;

    [Tooltip("全部角色列表")]
    public List<CharacterDefinition> characters = new List<CharacterDefinition>();

    public CharacterDefinition Get(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
            return GetDefault();

        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] != null && characters[i].characterId == characterId)
                return characters[i];
        }

        return GetDefault();
    }

    public CharacterDefinition GetDefault()
    {
        if (defaultCharacter != null)
            return defaultCharacter;
        if (characters.Count > 0)
            return characters[0];
        return null;
    }
}
