using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skills/Skill Catalog", fileName = "SkillCatalog")]
public class SkillCatalog : ScriptableObject
{
    public List<SkillDefinitionBase> skills = new List<SkillDefinitionBase>();

    public SkillDefinitionBase Get(SkillId id)
    {
        for (int i = 0; i < skills.Count; i++)
        {
            var d = skills[i];
            if (d == null) continue;
            if (d.id == id) return d;
        }
        return null;
    }

    public IEnumerable<SkillDefinitionBase> All()
    {
        for (int i = 0; i < skills.Count; i++)
        {
            var d = skills[i];
            if (d != null) yield return d;
        }
    }
}

