using UnityEngine;

/// <summary>订阅 <see cref="EventBus"/>，自动播放战斗/UI 相关音效。</summary>
[DisallowMultipleComponent]
public sealed class AudioEventBridge : MonoBehaviour
{
    private void OnEnable()
    {
        EventBus.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged, owner: this);
        EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied, owner: this);
        EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged, owner: this);
        EventBus.Subscribe<SkillCastEvent>(OnSkillCast, owner: this);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
        EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Unsubscribe<SkillCastEvent>(OnSkillCast);
    }

    private static void OnEnemyDamaged(EnemyDamagedEvent e) =>
        AudioService.Ensure().Play(AudioId.EnemyHit);

    private static void OnEnemyDied(EnemyDiedEvent e) =>
        AudioService.Ensure().Play(AudioId.EnemyDie);

    private static void OnPlayerDamaged(PlayerDamagedEvent e) =>
        AudioService.Ensure().Play(AudioId.PlayerHurt);

    private static void OnSkillCast(SkillCastEvent e)
    {
        if (e.skillId == SkillId.OrbitingBlades)
            return;

        AudioId id = AudioCatalog.ResolveSkillAudioId(e.skillId);
        if (id != AudioId.None)
        {
            AudioService service = AudioService.Ensure();
            if (service != null)
                service.Play(id);
        }
    }
}
