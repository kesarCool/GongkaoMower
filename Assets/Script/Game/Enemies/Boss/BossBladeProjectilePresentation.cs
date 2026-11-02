using UnityEngine;

/// <summary>Optional Boss blade appearance; damage and lifetime remain in BossBullet.</summary>
[DisallowMultipleComponent]
public sealed class BossBladeProjectilePresentation : MonoBehaviour, IPoolReceiver
{
    [SerializeField] private GameObject dissolvePrefab;
    TrailRenderer[] trails;
    bool dissolved;
    void Awake() { trails=GetComponentsInChildren<TrailRenderer>(true); }
    void OnEnable() { dissolved=false;ResetTrails(true); }
    void OnDisable() { ResetTrails(false); }
    public void OnPoolGet() { dissolved=false;ResetTrails(true); }
    public void OnPoolRelease() { ResetTrails(false); }
    public void PlayDissolve(Vector2 position)
    {
        if(dissolved || dissolvePrefab==null || !Application.isPlaying)return;
        dissolved=true;
        CombatVfxSpawner.TryPlayPooled(dissolvePrefab,new Vector3(position.x,position.y,-.15f),Quaternion.identity,.3f);
    }
    void ResetTrails(bool emitting)
    {
        if(trails==null)trails=GetComponentsInChildren<TrailRenderer>(true);
        for(int i=0;i<trails.Length;i++)
        {
            if(trails[i]==null)continue;
            trails[i].emitting=false;trails[i].Clear();trails[i].emitting=emitting;
        }
    }
}
