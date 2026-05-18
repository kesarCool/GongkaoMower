using UnityEngine;

/// <summary>
/// 技能基类：管理等级与装备状态
/// </summary>
public abstract class SkillBase : ISkill
{
    public SkillId Id { get; protected set; }
    public int Level { get; protected set; } = 1;

    protected SkillContext _ctx;
    protected bool _equipped;

    public virtual void OnEquip(SkillContext ctx)
    {
        _ctx = ctx;
        _equipped = true;
    }

    public virtual void OnUnequip()
    {
        _equipped = false;
    }

    public virtual void OnLevelUp()
    {
        Level = Mathf.Max(1, Level + 1);
    }

    public abstract void Tick(float deltaTime);

    protected static GameObject FindNearestEnemy(Vector3 from, string enemyTag, float maxRange = 9999f)
    {
        GameObject[] enemies;
        try
        {
            enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        }
        catch (UnityException)
        {
            return null;
        }

        if (enemies == null || enemies.Length == 0) return null;

        float best = float.PositiveInfinity;
        GameObject bestObj = null;
        float maxSq = maxRange * maxRange;

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null) continue;
            float d = (enemies[i].transform.position - from).sqrMagnitude;
            if (d > maxSq) continue;
            if (d < best)
            {
                best = d;
                bestObj = enemies[i];
            }
        }

        return bestObj;
    }

    protected static int CountActiveEnemiesInRange(Vector3 from, string enemyTag, float maxRange)
    {
        if (string.IsNullOrEmpty(enemyTag))
            return 0;

        GameObject[] enemies;
        try
        {
            enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        }
        catch (UnityException)
        {
            return 0;
        }

        if (enemies == null || enemies.Length == 0)
            return 0;

        float maxSq = maxRange * maxRange;
        int count = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null || !enemies[i].activeInHierarchy)
                continue;

            float d = (enemies[i].transform.position - from).sqrMagnitude;
            if (d <= maxSq)
                count++;
        }

        return count;
    }

    protected static Vector2 RandomDirection2D()
    {
        float rad = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}
