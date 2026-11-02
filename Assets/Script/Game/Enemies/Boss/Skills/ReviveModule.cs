using System.Collections;
using UnityEngine;

/// <summary>Original table revive timing and HP formula, with owned glyph presentation and an actual reusable shield.</summary>
public class ReviveModule:BossSkillModule
{
    private float _reviveHpPercent=.5f,_reviveDelay=1.5f,_invincibleDuration=.5f;
    private int _maxRevives=1,_reviveCount,_generation;
    private EnemyBase _eb;
    private BossRevivePresentation _presentation;
    private Coroutine _sequence;
    private ResistShield _ownedShield;
    private bool _disposed;
    public bool IsReviving {get;private set;}
    public override bool IsPassive=>true;
    public override bool CanTrigger()=>false;
    public override void Execute(){}
    public override void Init(string rawParams,BossBrain owner)
    {
        base.Init(rawParams,owner);var p=ParseFloats(rawParams,3);
        _reviveHpPercent=Mathf.Clamp01(p[0]>0?p[0]:.5f);
        _reviveDelay=Mathf.Max(.1f,p[1]>0?p[1]:1.5f);
        _maxRevives=Mathf.Max(1,(int)(p[2]>0?p[2]:1));
        _eb=owner.GetComponent<EnemyBase>();_presentation=owner.GetComponentInChildren<BossRevivePresentation>(true);
        if(_eb!=null)_eb.OnDied.AddListener(OnBossDied);
    }
    void OnBossDied()
    {
        if(_disposed||_eb==null||boss==null||!boss.gameObject.activeInHierarchy)return;
        // A second lethal hit during the original, vulnerable recovery window must
        // replace the old sequence or really die when no charges remain.
        CancelSequence(false);
        if(_reviveCount>=_maxRevives){_eb.preventPoolDeath=false;return;}
        _reviveCount++;_eb.preventPoolDeath=true;IsReviving=true;brain.IsBusy=true;
        int generation=++_generation;_sequence=brain.StartCoroutine(DeathAndReviveSequence(generation));
    }
    bool Current(int generation)=>!_disposed&&generation==_generation&&boss!=null&&boss.gameObject.activeInHierarchy&&_eb!=null;
    IEnumerator DeathAndReviveSequence(int generation)
    {
        var blade=boss.GetComponentInChildren<BossBladeBurstPresentation>(true);if(blade!=null)blade.Cancel();
        var dash=boss.GetComponentInChildren<BossDashPresentation>(true);if(dash!=null)dash.Cancel();
        var knife=boss.GetComponentInChildren<BossKnifePresentation>(true);if(knife!=null)knife.Cancel();
        if(_presentation!=null)_presentation.BeginDeath();
        yield return null;
        float waited=0;
        while(waited<_reviveDelay&&Current(generation))
        {
            waited+=Time.deltaTime;if(_presentation!=null)_presentation.SetWaitProgress(waited/_reviveDelay);yield return null;
        }
        if(!Current(generation))yield break;
        float newHp=_eb.MaxHp*_reviveHpPercent;
        _eb.ApplyTableStats(0,Mathf.CeilToInt(newHp),0);_eb.ResetForPool();_eb.ShowFromRevive();
        if(_presentation!=null)_presentation.BeginRecovery();
        float recovery=0;
        while(recovery<.35f&&Current(generation))
        {
            recovery+=Time.deltaTime;if(_presentation!=null)_presentation.SetRecoveryProgress(recovery/.35f);yield return null;
        }
        if(!Current(generation))yield break;
        if(_presentation!=null)_presentation.FinishRecovery();
        _ownedShield=boss.GetComponent<ResistShield>();if(_ownedShield==null)_ownedShield=boss.gameObject.AddComponent<ResistShield>();
        _ownedShield.SetupReusable(1,new[]{SkillDamageType.Physical,SkillDamageType.Energy,SkillDamageType.Explosive},0,_invincibleDuration);
        _eb.InvalidateResistShieldCache();_eb.preventPoolDeath=false;IsReviving=false;brain.IsBusy=false;_sequence=null;
        if(_presentation!=null)_presentation.BindProtection(_ownedShield);
        GameLog.Info($"[ReviveModule] Boss '{boss.name}' revived ({_reviveCount}/{_maxRevives}), HP={newHp:F0}");
    }
    void CancelSequence(bool restoreBody)
    {
        _generation++;if(_sequence!=null&&brain!=null)brain.StopCoroutine(_sequence);_sequence=null;
        if(_ownedShield!=null){_ownedShield.Cancel();_ownedShield=null;}
        if(_presentation!=null)_presentation.Cancel();
        if(IsReviving&&brain!=null)brain.IsBusy=false;IsReviving=false;
        if(_eb!=null)
        {
            if(restoreBody&&_eb.preventPoolDeath)_eb.ShowFromRevive();
            _eb.InvalidateResistShieldCache();
        }
    }
    public void OnBossDestroyed()
    {
        if(_disposed)return;_disposed=true;CancelSequence(true);
        if(_eb!=null){_eb.preventPoolDeath=false;_eb.OnDied.RemoveListener(OnBossDied);_eb=null;}
    }
}
