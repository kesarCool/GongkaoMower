/// <summary>音效 Id（业务只引用枚举，资源路径见 <see cref="AudioCatalog"/>）。</summary>
public enum AudioId
{
    None = 0,

    UiClick = 1,
    UiClose = 2,
    UiSwitch = 3,

    EnemyHit = 10,
    EnemyDie = 11,
    PlayerHurt = 12,

    SkillAutoProjectile = 101,
    SkillLineBeam = 102,
    SkillOrbitingBlades = 103,
    SkillThrowGrenade = 104,
    SkillFieldGenerator = 105,
    SkillLightningStrike = 106,
    SkillAutoProjectileSword = 107,

    SkillAutoProjectileTalisman = 108,

    BgmGame = 200,
}
