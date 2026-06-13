using System.Collections;
using UnityEngine;

/// <summary>
/// 召唤技能：Boss 触发 LevelWave 表中预先配置的待召唤波次（wave=0 等）。
/// 召唤怪的攻血速防全走 Excel 配表，生成逻辑复用 SpawnerWaves 的环形分散。
///
/// elementNum = "12,0" = cooldown, reserveWaveId
/// </summary>
public class SummonModule : BossSkillModule
{
    private int _reserveWaveId;
    private float _chargeTime = 0.45f;

    private SpawnerWaves _spawner;

    public override void Init(string rawParams, BossBrain owner)
    {
        base.Init(rawParams, owner);
        firstDelayMul = 0.5f;

        float[] p = ParseFloats(rawParams, 2);
        interval       = p[0] > 0f ? p[0] : 12f;
        _reserveWaveId = (int)(p[1] >= 0f ? p[1] : 0f);
        cooldown       = interval * firstDelayMul;

        _spawner = Object.FindObjectOfType<SpawnerWaves>();
        if (_spawner == null)
            Debug.LogWarning($"[SummonModule] 场景中无 SpawnerWaves，召唤技能不会触发。Boss='{boss?.name}'");
        CacheSprites();
    }

    public override bool CanTrigger()
    {
        return base.CanTrigger() && _spawner != null;
    }

    public override void Execute()
    {
        ResetCooldown();
        brain.StartCoroutine(SummonRoutine());
    }

    private IEnumerator SummonRoutine()
    {
        brain.IsBusy = true;
        SetSpritesFlash(true, new Color(0.7f, 0.3f, 1f, 1f));
        yield return new WaitForSeconds(_chargeTime);
        SetSpritesFlash(false);

        _spawner.TriggerReserveWave(_reserveWaveId);

        if (brain != null) brain.IsBusy = false;
    }
}
