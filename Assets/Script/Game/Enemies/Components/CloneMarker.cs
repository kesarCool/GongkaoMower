using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Clone identity, persistent skill policy and scaled-time lifetime. Never owns a boss-pool lease.</summary>
[DisallowMultipleComponent]
public sealed class CloneMarker : MonoBehaviour
{
    readonly HashSet<string> skills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    EnemyBase enemy, owner;
    bool configured, exiting;
    float bornAt, expiresAt;
    public EnemyBase Owner => owner;
    public bool IsConfigured => configured;
    public float BornAt => bornAt;
    public float ExpiresAt => expiresAt;
    public float RemainingSeconds => Mathf.Max(0, expiresAt - Time.time);
    public bool AllowsSkill(string type)
    {
        if (string.Equals(type,"clone",StringComparison.OrdinalIgnoreCase) || string.Equals(type,"revive",StringComparison.OrdinalIgnoreCase)) return false;
        return !configured || skills.Contains(type);
    }
    public void Configure(EnemyBase original,IEnumerable<string> inherited,float lifetime)
    {
        owner=original;skills.Clear();if(inherited!=null)foreach(var type in inherited)skills.Add(type);
        configured=true;exiting=false;bornAt=Time.time;expiresAt=bornAt+Mathf.Max(.01f,lifetime);
        var pool=GetComponent<PooledObject>();if(pool!=null)pool.sourcePrefabId=0;
        var view=GetComponentInChildren<BossClonePresentation>(true);if(view!=null)view.ConfigureClone(this);
    }
    void OnEnable(){enemy=GetComponent<EnemyBase>();if(enemy!=null)enemy.OnDied.AddListener(OnCloneDied);}
    void OnDisable(){if(enemy!=null)enemy.OnDied.RemoveListener(OnCloneDied);if(configured&&!exiting)Despawn(false);}
    void OnCloneDied(){if(exiting)return;exiting=true;var view=GetComponentInChildren<BossClonePresentation>(true);if(view!=null)view.PlayExitFeedback();}
    void Update()
    {
        if(!configured||exiting)return;
        if(owner==null||!owner.gameObject.activeInHierarchy){Despawn(false);return;}
        if(Time.time>=expiresAt)Despawn(true);
    }
    public void Despawn(bool feedback)
    {
        if(exiting)return;exiting=true;
        var view=GetComponentInChildren<BossClonePresentation>(true);if(feedback&&view!=null)view.PlayExitFeedback();
        foreach(var collider in GetComponentsInChildren<Collider2D>())collider.enabled=false;
        var body=GetComponent<Rigidbody2D>();if(body!=null){body.velocity=Vector2.zero;body.simulated=false;}
        Destroy(gameObject);
    }
}
