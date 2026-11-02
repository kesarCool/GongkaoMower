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
    private BossSummonPresentation _presentation;
    private EnemyBase _enemy;
    private Coroutine _routine;
    private bool _disposed;

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
        _presentation=owner.GetComponentInChildren<BossSummonPresentation>(true);
        _enemy=owner.GetComponent<EnemyBase>();
        if(_enemy!=null)_enemy.OnDied.AddListener(CancelPending);
    }

    public override bool CanTrigger()
    {
        return base.CanTrigger() && Ready();
    }

    public override void Execute()
    {
        if(!Ready()||_routine!=null)return;
        ResetCooldown();
        _routine=brain.StartCoroutine(SummonRoutine());
    }

    private IEnumerator SummonRoutine()
    {
        brain.IsBusy = true;
        int castToken=_presentation!=null?_presentation.BeginCharge(_chargeTime):0;
        SetSpritesFlash(true, new Color(1f, .35f, .08f, 1f));
        yield return new WaitForSeconds(_chargeTime);
        SetSpritesFlash(false);

        _routine=null;
        if(!Ready()){if(_presentation!=null)_presentation.CancelCharge();if(brain!=null)brain.IsBusy=false;yield break;}
        if(_presentation!=null)_presentation.ReleaseCharge(castToken);
        bool released=_spawner.TriggerReserveWave(_reserveWaveId,spawned=>{if(_presentation!=null)_presentation.OnSummoned(spawned,castToken);});
        if(!released&&_presentation!=null)_presentation.CancelCharge();

        if (brain != null) brain.IsBusy = false;
    }

    bool Ready()=>!_disposed&&boss!=null&&boss.gameObject.activeInHierarchy&&_spawner!=null&&_spawner.isActiveAndEnabled&&(_enemy==null||_enemy.Hp>0);
    void CancelPending()
    {
        if(_routine!=null&&brain!=null){brain.StopCoroutine(_routine);brain.IsBusy=false;}
        _routine=null;SetSpritesFlash(false);if(_presentation!=null)_presentation.CancelCharge();
    }
    public void OnOwnerDisposed()
    {
        if(_disposed)return;_disposed=true;CancelPending();
        if(_enemy!=null){_enemy.OnDied.RemoveListener(CancelPending);_enemy=null;}
    }
}
