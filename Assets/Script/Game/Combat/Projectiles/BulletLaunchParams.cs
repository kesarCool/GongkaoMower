/// <summary>
/// PlayerBullet.Launch 参数包，避免签名膨胀。
/// </summary>
public struct BulletLaunchParams
{
    public float Speed;
    public float Damage;
    public float Lifetime;
    public SkillId Source;
    public int PierceCount;
    public bool IsCrit;
    public float PierceRate;

    public BulletLaunchParams(float speed, float damage, float lifetime, SkillId source,
        int pierceCount = 0, bool isCrit = false, float pierceRate = 0f)
    {
        Speed = speed;
        Damage = damage;
        Lifetime = lifetime;
        Source = source;
        PierceCount = pierceCount;
        IsCrit = isCrit;
        PierceRate = pierceRate;
    }
}
