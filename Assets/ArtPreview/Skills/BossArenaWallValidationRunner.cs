#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Real boss-hit/death events, actual arena colliders, isolated profile, and captured world rendering.</summary>
public sealed class BossArenaWallValidationRunner : UIValidationRunnerBase
{
    bool Baseline => SessionState.GetBool("BossWallBaseline",false);
    protected override string OutputFolder => SessionState.GetString("BossWallPreviewPrefix","BossWall")+(Baseline?"Before":"After");
    protected override string ValidationNote => "Actual Game scene and boss-hit/death event path. Isolated in-memory save. Overview camera is temporary for inspection; gameplay camera uses original zoom. No WeChat device or final build-size validation.";
    UnityEngine.Random.State savedRandom;
    float savedScale;
    bool captured;
    bool previousBackground;
    void Awake(){previousBackground=Application.runInBackground;Application.runInBackground=true;}

    protected override IEnumerator RunChecks()
    {
        savedRandom=UnityEngine.Random.state;savedScale=Time.timeScale;captured=true;
        yield return SceneManager.UnloadSceneAsync("Home");SelectedLevelContext.Set(1,101);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));yield return new WaitForSecondsRealtime(.4f);
        foreach(var spawner in FindObjectsOfType<SpawnerWaves>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()){outcome.StopAllCoroutines();outcome.enabled=false;}
        foreach(var follow in FindObjectsOfType<CameraFollow2D>())follow.enabled=false;
        UIManager.Instance.CloseAllStack();Time.timeScale=1;
        foreach(var enemy in FindObjectsOfType<EnemyBase>())Destroy(enemy.gameObject);
        yield return null;CombatTargetRegistry.ClearAll();
        var skills=FindObjectOfType<PlayerSkills>();skills.ClearAllSkills();skills.enabled=false;
        var player=skills.transform;player.GetComponent<PlayerController>().enabled=false;
        var playerBody=player.GetComponent<Rigidbody2D>();
        if(playerBody!=null){playerBody.velocity=Vector2.zero;playerBody.gravityScale=0;}
        player.position=new Vector3(14,14,0);
        var camera=Camera.main;float gameplayZoom=camera.orthographicSize;
        camera.transform.position=new Vector3(14,14,-10);camera.orthographicSize=16.5f;
        var arena=FindObjectOfType<BossArenaLock>();
        if(Baseline)arena.wallPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(SessionState.GetString("BossWallBaselinePrefabPath","Assets/Prefab/Battle/wall.prefab"));
        Record("Game supplies the configured 16 by 20 boss arena",arena!=null&&arena.arenaHalfWidth==8&&arena.arenaHalfHeight==10&&!arena.IsLocked);

        EnemyBase Spawn(string path,Vector2 pos,string word)
        {
            var go=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),pos,Quaternion.identity);
            foreach(var behavior in go.GetComponentsInChildren<MonoBehaviour>())
                if(!(behavior is EnemyBase)&&!(behavior is EnemyWordLabel)&&!(behavior is TMP_Text))behavior.enabled=false;
            var body=go.GetComponent<Rigidbody2D>();if(body!=null){body.bodyType=RigidbodyType2D.Kinematic;body.velocity=Vector2.zero;}
            var enemy=go.GetComponent<EnemyBase>();enemy.ApplyTableStats(1,10000,0,0);
            var label=go.GetComponentInChildren<EnemyWordLabel>();if(label!=null)label.SetWord(word);
            return enemy;
        }
        var normal=Spawn("Assets/Prefab/Enemy/Normal/enemy001.prefab",new Vector2(11,13),"北京烤鸭");
        normal.TakeDamage(1);Record("A normal enemy hit does not create boss walls",!arena.IsLocked);
        var boss=Spawn("Assets/Prefab/Enemy/Boss/boss_30001.prefab",new Vector2(28,28),"饕餮");
        var marker=boss.GetComponent<LastWaveBossMarker>();if(marker==null)marker=boss.gameObject.AddComponent<LastWaveBossMarker>();marker.isFinalBoss=false;
        boss.TakeDamage(1);
        var walls=GameObject.Find("BossArenaWalls");
        Record("A real boss damage event creates exactly four wall roots",arena.IsLocked&&walls!=null&&walls.transform.childCount==4);
        Record("Boss is pulled into padded bounds around the map center",boss.transform.position.x<=22.21f&&boss.transform.position.y<=24.21f&&boss.transform.position.x>=7.79f&&boss.transform.position.y>=5.79f);
        boss.transform.position=new Vector3(16.5f,18,0);Physics2D.SyncTransforms();
        if(walls==null)yield break;
        var colliders=walls.GetComponentsInChildren<BoxCollider2D>();
        Record("Only the four original static physical blockers exist",colliders.Length==4&&walls.GetComponentsInChildren<Rigidbody2D>().All(r=>r.bodyType==RigidbodyType2D.Static)&&walls.GetComponentsInChildren<WallMarker>().Length==4);
        Record("Original 0.4-unit collision thickness and dimensions are retained",colliders.All(c=>c.name=="WallTop"||c.name=="WallBottom"?c.size==new Vector2(16,.4f):c.size==new Vector2(.4f,20)));
        bool covered=true;
        foreach(var col in colliders)
        {
            Vector2 inward=(new Vector2(14,14)-(Vector2)col.transform.position).normalized;
            var hits=Physics2D.RaycastAll((Vector2)col.transform.position+inward, -inward,2);
            covered&=hits.Any(h=>h.collider==col);
        }
        Record("All four boundaries are queryable physical walls",covered);
        if(!Baseline)
        {
            var views=walls.GetComponentsInChildren<BossArenaWallVisual>();
            if(views.Any(v=>v.tileSprite!=null))
            {
                bool tileMode=views.Length==4&&views.All(v=>v.tileSprite!=null);
                Record("Every wall consistently uses the single-sprite tile mode",tileMode);
                if(tileMode)ValidateStoneMeshes(walls,views);
            }
            else
            {
                Record("Every wall uses the new directional art prefab",views.Length==4&&views.All(v=>v.horizontalSprite!=null&&v.verticalSprite!=null));
                Record("Horizontal and vertical walls use the matching sprite without rotation",views.All(v=>v.transform.Find("WallArt").GetComponent<SpriteRenderer>().sprite==(v.name=="WallTop"||v.name=="WallBottom"?v.horizontalSprite:v.verticalSprite)));
                var renders=walls.GetComponentsInChildren<SpriteRenderer>();
                Record("All wall art shares one atlas and has no per-frame animator",renders.All(r=>r.sprite.texture==renders[0].sprite.texture)&&walls.GetComponentsInChildren<Animator>().Length==0);
                bool usesPosts=arena.wallPrefab.GetComponent<BossArenaWallVisual>().postSprite!=null;
                Record("Post configuration is respected without adding any visual colliders",renders.Count(r=>r.name=="CornerPost")== (usesPosts?4:0)&&colliders.Length==4&&(usesPosts||renders.All(r=>r.name=="WallArt")));
                int children=walls.GetComponentsInChildren<Transform>().Length;
                foreach(var view in views)view.Configure(view.GetComponent<BoxCollider2D>().size);
                Record("Configuring an existing wall does not duplicate render objects",walls.GetComponentsInChildren<Transform>().Length==children);
            }
        }
        yield return new WaitForSeconds(.95f);yield return Capture("overview-1080x1920");
        camera.orthographicSize=gameplayZoom;camera.transform.position=new Vector3(9.5f,12,-10);
        player.position=new Vector3(9.5f,10.5f,0);yield return Capture("gameplay-1080x1920");
        // Center remains fixed throughout a boss fight; the collider must not follow the player.
        Record("Moving the player leaves all wall roots fixed",Vector2.Distance(walls.transform.Find("WallLeft").position,new Vector2(7,15))<.001f);
        EventBus.Publish(new EnemyDiedEvent{enemy=normal,position=normal.transform.position});
        Record("Unrelated enemy death does not remove boss walls",arena.IsLocked);
        EventBus.Publish(new EnemyDiedEvent{enemy=boss,position=boss.transform.position});yield return null;
        Record("Defeated boss removes the visual wall and blockers together",!arena.IsLocked&&GameObject.Find("BossArenaWalls")==null);
        camera.orthographicSize=16.5f;camera.transform.position=new Vector3(14,14,-10);player.position=new Vector3(14,14,0);
        boss.TakeDamage(1);yield return null;
        Record("A subsequent boss lock rebuilds a single four-wall arena",arena.IsLocked&&GameObject.Find("BossArenaWalls").transform.childCount==4);
        arena.enabled=false;yield return null;
        Record("Disabling the arena also cleans all wall resources",!arena.IsLocked&&GameObject.Find("BossArenaWalls")==null);
        Destroy(normal.gameObject);Destroy(boss.gameObject);Time.timeScale=savedScale;UnityEngine.Random.state=savedRandom;
    }
    void ValidateStoneMeshes(GameObject walls,BossArenaWallVisual[] views)
    {
        const float epsilon=.0001f;
        var renderers=walls.GetComponentsInChildren<MeshRenderer>();
        var filters=walls.GetComponentsInChildren<MeshFilter>();
        bool complete=renderers.Length==4&&filters.Length==4&&filters.All(f=>f.sharedMesh!=null)
            &&views.All(v=>v.GetComponentsInChildren<MeshRenderer>().Length==1&&v.GetComponentsInChildren<MeshFilter>().Length==1);
        Record("Four merged tile meshes retain exactly four physical colliders",complete
            &&walls.GetComponentsInChildren<Collider2D>().Length==4&&walls.GetComponentsInChildren<SpriteRenderer>().Length==0
            &&walls.GetComponentsInChildren<Animator>().Length==0);
        Record("The 16-by-20 arena at half-unit pitch contains 144 complete tiles",views.All(v=>Mathf.Abs(v.tilePitch-.5f)<epsilon)&&views.Sum(v=>v.TileCount)==144);
        if(!complete)return;
        var tile=views[0].tileSprite;var material=renderers[0].sharedMaterial;
        bool shared=material!=null&&views.All(v=>v.tileSprite==tile)
            &&renderers.All(r=>r.sharedMaterial==material&&r.sharedMaterials.Length==1);
        foreach(var view in views)
        {
            var renderer=view.GetComponentInChildren<MeshRenderer>();
            if(view.tileMaterial!=null)shared&=renderer.sharedMaterial==view.tileMaterial;
            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            shared&=block.GetTexture("_MainTex")==tile.texture;
        }
        Record("All tile meshes reuse one texture and the same shared material",shared);
        var centers=new List<Vector2>();var rectangles=new List<Rect>();bool fullQuads=true;
        foreach(var view in views)
        {
            var filter=view.GetComponentInChildren<MeshFilter>();var mesh=filter.sharedMesh;
            var vertices=mesh.vertices;var uv=mesh.uv;var triangles=mesh.triangles;
            bool layout=vertices.Length==view.TileCount*4&&uv.Length==vertices.Length&&triangles.Length==view.TileCount*6;
            fullQuads&=layout;if(!layout)continue;
            Vector2 size=(Vector2)view.tileSprite.bounds.size*Mathf.Max(.01f,view.tileScale);Rect source=view.tileSprite.textureRect;
            var expectedUv=new Rect(source.xMin/tile.texture.width,source.yMin/tile.texture.height,source.width/tile.texture.width,source.height/tile.texture.height);
            for(int i=0;i<vertices.Length;i+=4)
            {
                var local=vertices.Skip(i).Take(4).ToArray();var tex=uv.Skip(i).Take(4).ToArray();
                float x0=local.Min(p=>p.x),x1=local.Max(p=>p.x),y0=local.Min(p=>p.y),y1=local.Max(p=>p.y);
                fullQuads&=Mathf.Abs(x1-x0-size.x)<epsilon&&Mathf.Abs(y1-y0-size.y)<epsilon
                    &&local.All(p=>(Mathf.Abs(p.x-x0)<epsilon||Mathf.Abs(p.x-x1)<epsilon)&&(Mathf.Abs(p.y-y0)<epsilon||Mathf.Abs(p.y-y1)<epsilon))
                    &&local.Distinct().Count()==4&&triangles.Skip(i/4*6).Take(6).All(t=>t>=i&&t<i+4)
                    &&Mathf.Abs(tex.Min(p=>p.x)-expectedUv.xMin)<epsilon&&Mathf.Abs(tex.Max(p=>p.x)-expectedUv.xMax)<epsilon
                    &&Mathf.Abs(tex.Min(p=>p.y)-expectedUv.yMin)<epsilon&&Mathf.Abs(tex.Max(p=>p.y)-expectedUv.yMax)<epsilon;
                var world=local.Select(p=>filter.transform.TransformPoint(p)).ToArray();
                float wx0=world.Min(p=>p.x),wx1=world.Max(p=>p.x),wy0=world.Min(p=>p.y),wy1=world.Max(p=>p.y);
                centers.Add(new Vector2((wx0+wx1)*.5f,(wy0+wy1)*.5f));rectangles.Add(Rect.MinMaxRect(wx0,wy0,wx1,wy1));
            }
        }
        Record("Every tile quad retains its configured uniform scale and full texture region",fullQuads&&centers.Count==144);
        bool nonOverlapping=true;
        for(int i=0;i<rectangles.Count;i++)for(int j=i+1;j<rectangles.Count;j++)
            if(Mathf.Min(rectangles[i].xMax,rectangles[j].xMax)-Mathf.Max(rectangles[i].xMin,rectangles[j].xMin)>epsilon
                &&Mathf.Min(rectangles[i].yMax,rectangles[j].yMax)-Mathf.Max(rectangles[i].yMin,rectangles[j].yMin)>epsilon)nonOverlapping=false;
        float left=walls.transform.Find("WallLeft").position.x,right=walls.transform.Find("WallRight").position.x;
        float bottom=walls.transform.Find("WallBottom").position.y,top=walls.transform.Find("WallTop").position.y;
        var corners=new[]{new Vector2(left,bottom),new Vector2(left,top),new Vector2(right,bottom),new Vector2(right,top)};
        Record("Each corner owns exactly one complete tile and no tile quads overlap",nonOverlapping&&corners.All(c=>centers.Count(p=>Vector2.Distance(c,p)<epsilon)==1));
        int children=walls.GetComponentsInChildren<Transform>().Length;
        var meshes=filters.Select(f=>f.sharedMesh.GetInstanceID()).ToArray();var counts=views.Select(v=>v.TileCount).ToArray();
        foreach(var view in views)view.Configure(view.GetComponent<BoxCollider2D>().size);
        Record("Repeated Configure reuses the existing meshes, material, tile counts and colliders",walls.GetComponentsInChildren<Transform>().Length==children
            &&walls.GetComponentsInChildren<MeshRenderer>().Length==4&&walls.GetComponentsInChildren<Collider2D>().Length==4
            &&filters.Select(f=>f.sharedMesh.GetInstanceID()).SequenceEqual(meshes)&&views.Select(v=>v.TileCount).SequenceEqual(counts)
            &&renderers.All(r=>r.sharedMaterial==material));
    }
    protected override void OnDestroy()
    {
        Application.runInBackground=previousBackground;
        if(captured){Time.timeScale=savedScale;UnityEngine.Random.state=savedRandom;}
        base.OnDestroy();
    }
}
#endif
