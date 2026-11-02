using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Ground fissure: locked-origin fan, independent 0.7-second warnings, sequential impacts.
/// elementNum = cooldown, count (2..32), radius, damage. No Monster row is reassigned by the presentation builder.</summary>
public class ZoneModule : BossSkillModule
{
    private int _zoneCount=5;
    private float _zoneRadius=1.5f,_damage=30f;
    private float _warnDuration=.7f,_coneAngle=150f,_spreadLength=6f;
    private Rigidbody2D _rb;EnemyBase _enemy;BossZonePresentation _presentation;
    private Coroutine _routine;bool _disposed;
    readonly HashSet<int> _hitSet=new HashSet<int>();
    readonly Vector2[] _positions=new Vector2[BossZonePresentation.Capacity];
    readonly float[] _warnAt=new float[BossZonePresentation.Capacity];
    public int ImpactCount {get;private set;}
    public float LastImpactTime {get;private set;}
    public float CastTime {get;private set;}
    public Vector2 LastImpactPosition {get;private set;}
    public override void Init(string rawParams,BossBrain owner)
    {
        base.Init(rawParams,owner);firstDelayMul=.5f;float[] p=ParseFloats(rawParams,4);
        interval=p[0]>0?p[0]:10;_zoneCount=Mathf.Clamp((int)(p[1]>0?p[1]:5),2,BossZonePresentation.Capacity);
        _zoneRadius=p[2]>0?p[2]:1.5f;_damage=p[3]>0?p[3]:30;cooldown=interval*firstDelayMul;
        _rb=boss.GetComponent<Rigidbody2D>();_enemy=boss.GetComponent<EnemyBase>();_presentation=owner.GetComponentInChildren<BossZonePresentation>(true);
        if(_enemy!=null)_enemy.OnDied.AddListener(CancelPending);
        if(_presentation==null)Debug.LogWarning("[ZoneModule] Missing BossZonePresentation: refusing invisible ground-fissure damage on "+owner.name);
    }
    bool Ready()=>!_disposed&&brain!=null&&brain.isActiveAndEnabled&&boss.gameObject.activeInHierarchy&&(_enemy==null||_enemy.Hp>0)&&_presentation!=null&&_presentation.Ready;
    public override bool CanTrigger()=>base.CanTrigger()&&Ready()&&_routine==null;
    public override void Execute()
    {
        if(!Ready()||_routine!=null||brain.IsReviving)return;
        ResetCooldown();_routine=brain.StartCoroutine(ZoneRoutine());
    }
    IEnumerator ZoneRoutine()
    {
        brain.IsBusy=true;_presentation.CancelAll();_hitSet.Clear();ImpactCount=0;CastTime=Time.time;
        Vector2 center=_rb!=null?_rb.position:(Vector2)boss.position;
        Transform target=FindPlayer();Vector2 aim=target!=null?(Vector2)target.position-center:Vector2.down;if(aim.sqrMagnitude<.0001f)aim=Vector2.down;
        float baseAngle=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg,spacing=_warnDuration/_zoneCount;
        int warned=0;float damage=ResolveDamage(_damage,30);
        while(ImpactCount<_zoneCount)
        {
            if(!Ready()){CancelPending();yield break;}
            // A frame stall may delay release, but must never shorten a warning that was actually displayed.
            if(warned<_zoneCount&&Time.time-CastTime>=warned*spacing)
            {
                float t=(float)warned/(_zoneCount-1),angle=baseAngle+Mathf.Lerp(-_coneAngle*.5f,_coneAngle*.5f,t);
                float distance=Mathf.Lerp(1,_spreadLength,t*.7f+Random.value*.3f);
                _positions[warned]=center+new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad))*distance;
                _warnAt[warned]=Time.time;_presentation.Warn(warned,_positions[warned],_zoneRadius,_warnDuration);warned++;
            }
            if(ImpactCount<warned&&Time.time-_warnAt[ImpactCount]>=_warnDuration)
            {
                int index=ImpactCount;Vector2 point=_positions[index];
                // Retire the gameplay warning in the same frame as its one real overlap query.
                _presentation.Impact(index);LastImpactPosition=point;LastImpactTime=Time.time;ImpactCount++;
                ApplyHit(point,damage);
            }
            yield return null;
        }
        _routine=null;if(brain!=null)brain.IsBusy=false;
    }
    void ApplyHit(Vector2 point,float damage)
    {
        bool previous=Physics2D.queriesHitTriggers;
        try
        {
            Physics2D.queriesHitTriggers=true;
            foreach(var hit in Physics2D.OverlapCircleAll(point,_zoneRadius))
            {
                var player=hit.GetComponentInParent<PlayerHealth>();if(player==null||!_hitSet.Add(player.GetInstanceID()))continue;
                player.TakeDamage(damage,boss);
            }
        }
        finally{Physics2D.queriesHitTriggers=previous;}
    }
    void CancelPending()
    {
        if(_routine!=null&&brain!=null){brain.StopCoroutine(_routine);brain.IsBusy=brain.IsReviving;}_routine=null;
        if(_presentation!=null)_presentation.CancelAll();_hitSet.Clear();
    }
    public void OnOwnerDisposed()
    {
        if(_disposed)return;_disposed=true;CancelPending();if(_enemy!=null){_enemy.OnDied.RemoveListener(CancelPending);_enemy=null;}
    }
}
