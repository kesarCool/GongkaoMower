using UnityEngine;

/// <summary>
/// 在 Scene 视图绘制力场发生器伤害半径（与 <see cref="SkillFieldGenerator.radius"/> 一致，调试用）。
/// </summary>
[DisallowMultipleComponent]
public class FieldGeneratorRadiusGizmo : MonoBehaviour
{
    [SerializeField] private PlayerSkills playerSkills;

    [Tooltip("伤害圈线色（Scene 视图 Gizmos）")]
    public Color damageRadiusColor = new Color(0f, 1f, 1f, 0.9f);

    [Tooltip("Play 模式下未装备力场时是否仍按 SkillDef Lv.1 绘制参考圈")]
    public bool drawPreviewWhenUnequipped;

    private const int CircleSegments = 64;

    private void Reset()
    {
        playerSkills = GetComponent<PlayerSkills>();
    }

    private void Awake()
    {
        if (playerSkills == null)
            playerSkills = GetComponent<PlayerSkills>();
    }

    private void OnDrawGizmos()
    {
        if (playerSkills == null)
            playerSkills = GetComponent<PlayerSkills>();
        if (playerSkills == null) return;

        if (TryGetRuntimeRadius(out float radius))
        {
            Gizmos.color = damageRadiusColor;
            DrawWireCircle(transform.position, radius);
        }
        else if (drawPreviewWhenUnequipped && TryGetPreviewRadius(out radius))
        {
            Gizmos.color = new Color(damageRadiusColor.r, damageRadiusColor.g, damageRadiusColor.b, 0.35f);
            DrawWireCircle(transform.position, radius);
        }
    }

    private bool TryGetRuntimeRadius(out float radius)
    {
        radius = 0f;
        SkillFieldGenerator field = playerSkills.GetEquippedSkill<SkillFieldGenerator>(SkillId.FieldGenerator);
        if (field == null) return false;

        radius = field.radius;
        return radius > 0f;
    }

    private bool TryGetPreviewRadius(out float radius)
    {
        radius = 0f;
        if (playerSkills.skillCatalog == null) return false;

        var def = playerSkills.skillCatalog.Get(SkillId.FieldGenerator) as FieldGeneratorSkillDefinition;
        if (def == null) return false;

        radius = def.RadiusAt(1);
        return radius > 0f;
    }

    private static void DrawWireCircle(Vector3 center, float radius)
    {
        if (radius <= 0f) return;

        float step = Mathf.PI * 2f / CircleSegments;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= CircleSegments; i++)
        {
            float a = step * i;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
