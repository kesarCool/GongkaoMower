#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BattleReadabilityValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "BattleReadabilityV3";
    protected override string ValidationNote => "Actual Game scene and level 101/102 maps; representative pooled-prefab word and damage samples; isolated in-memory profile; no WeChat device validation.";
    readonly List<GameObject> samples = new List<GameObject>();
    protected override IEnumerator RunChecks()
    {
        bool baseline = SessionState.GetBool("BattleReadabilityBaseline", false);
        yield return SceneManager.UnloadSceneAsync("Home");
        SelectedLevelContext.Set(1,101);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
        yield return null; yield return null; yield return new WaitForSecondsRealtime(.4f);
        foreach(var spawner in FindObjectsOfType<SpawnerWaves>()){spawner.StopAllCoroutines();spawner.enabled=false;}
        foreach(var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()){outcome.StopAllCoroutines();outcome.enabled=false;}
        foreach(var follow in FindObjectsOfType<CameraFollow2D>())follow.enabled=false;
        Time.timeScale=0; Random.InitState(4169);
        Camera.main.transform.position=new Vector3(15,15,-10);Camera.main.orthographicSize=11.5f;
        var player=GameObject.FindGameObjectWithTag("Player");player.transform.position=new Vector3(15,12,0);
        var names=new[]{"北京烤鸭","炸酱面","豌豆黄","胡辣汤","牡丹燕菜","扬州炒饭"};
        var places=new[]{new Vector2(.25f,.68f),new Vector2(.68f,.73f),new Vector2(.53f,.57f),new Vector2(.23f,.42f),new Vector2(.72f,.35f),new Vector2(.53f,.24f)};
        var enemyPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Normal/enemy001.prefab");
        WordMonsterWaveStyle.ApplyWaveStart(WordMonsterWaveTintMode.SeededByLevelAndWave,101,2);
        for(int i=0;i<names.Length;i++)
        {
            var go=Instantiate(enemyPrefab,World(places[i]),Quaternion.identity);samples.Add(go);
            foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>()) if(!(behaviour is EnemyWordLabel) && !(behaviour is TMP_Text))behaviour.enabled=false;
            go.GetComponentInChildren<EnemyWordLabel>().SetWord(names[i]);
        }
        var damagePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/DamageFloatText.prefab");
        for(int i=0;i<4;i++)
        {
            var pos=World(new Vector2(.28f+i%2*.43f,.51f-i/2*.19f));var go=Instantiate(damagePrefab,pos,Quaternion.identity);samples.Add(go);
            var fx=go.GetComponent<DamageFloatText>();fx.OnPoolGet();if(i==3)fx.PlayResist(12,pos,false);else fx.Play(i==1?128:46,pos,i==1,i==2);
        }
        yield return Capture((baseline?"before":"after")+"-level-1-1-1080x1920");
        Record("Captured actual level 101 with representative labels",BattleMapLoader.Instance!=null && BattleMapLoader.Instance.GroundTilemap!=null);
        if(!baseline)
        {
            var hud=FindObjectOfType<GameLayer>();var gradient=hud.transform.Find("HudTop/TopGradient").GetComponent<UnityEngine.UI.Image>();
            Record("HUD tint is reduced without blocking input",Mathf.Abs(gradient.color.a-.82f)<.001f && !gradient.raycastTarget);
            ValidateMiniMap("1-1");
            var texts=samples.SelectMany(s=>s.GetComponentsInChildren<TMP_Text>()).ToArray();
            var font=BattleChineseFontRuntime.LoadedFont;
            Record("Words and damage reuse the existing Chinese font",texts.Length>=10 && texts.All(t=>t.font==font));
            Record("Text material presets reuse that font's atlas texture",texts.All(t=>t.fontSharedMaterial.mainTexture==font.atlasTexture));
            var wordLabels=samples.SelectMany(s=>s.GetComponentsInChildren<EnemyWordLabel>()).ToArray();
            var wordTexts=wordLabels.Select(w=>w.GetComponentInChildren<TMP_Text>()).ToArray();
            var material=wordTexts[0].fontSharedMaterial;
            Record("Six enemies share one outline material",wordTexts.All(t=>t.fontSharedMaterial==material));
            Color outline=material.GetColor("_OutlineColor");
            Record("Bright word faces have strong contrast against dark outline",material.GetFloat("_OutlineWidth")>=.3f && wordTexts.All(t=>Contrast(t.colorGradient.bottomLeft,outline)>7));
            var first=wordLabels[0];var tmp=wordTexts[0];var tint=tmp.colorGradient;
            first.PlayHitFeedback();yield return null;
            Record("Hit flash keeps shared material isolated",wordTexts.All(t=>t.fontSharedMaterial==material) && wordTexts[1].colorGradient.topLeft!=Color.white);
            first.enabled=false;yield return null;first.enabled=true;first.SetWord(names[0]);
            Record("Reused word restores its tint after hit flash",tmp.fontSharedMaterial==material && tmp.colorGradient.bottomLeft!=Color.white && Contrast(tmp.colorGradient.bottomLeft,outline)>7);
            var damageTexts=samples.Where(s=>s.GetComponent<DamageFloatText>()!=null).Select(s=>s.GetComponentInChildren<TMP_Text>()).ToArray();
            Record("Four damage types remain distinguishable with a shared outline",damageTexts.Select(t=>t.color).Distinct().Count()==4 && damageTexts.Select(t=>t.fontSharedMaterial).Distinct().Count()==1);
            Record("Existing world health text uses the same font",player.GetComponentsInChildren<TMP_Text>().All(t=>t.font==font));
            WordMonsterWaveStyle.ApplyWaveStart(WordMonsterWaveTintMode.SeededByLevelAndWave,101,4);var seeded=WordMonsterWaveStyle.WaveFaceTint;
            WordMonsterWaveStyle.ApplyWaveStart(WordMonsterWaveTintMode.SeededByLevelAndWave,101,4);
            Record("Wave palette remains deterministic for seeded mode",seeded==WordMonsterWaveStyle.WaveFaceTint);
            WordMonsterWaveStyle.ClearWaveTint();WordMonsterWaveStyle.ApplyWaveStart(WordMonsterWaveTintMode.Off,101,1);
            Record("Wave tint Off still disables wave override",!WordMonsterWaveStyle.HasWaveTint);
            foreach(var sample in samples)Destroy(sample);samples.Clear();
            Camera.main.transform.position=new Vector3(6.5f,18,-10);player.transform.position=new Vector3(6.5f,15,0);
            WordMonsterWaveStyle.ApplyWaveStart(WordMonsterWaveTintMode.SeededByLevelAndWave,101,2);
            for(int i=0;i<3;i++)
            {
                var go=Instantiate(enemyPrefab,World(new Vector2(.25f+i*.24f,.68f-i*.15f)),Quaternion.identity);samples.Add(go);
                foreach(var b in go.GetComponentsInChildren<MonoBehaviour>())if(!(b is EnemyWordLabel) && !(b is TMP_Text))b.enabled=false;
                go.GetComponentInChildren<EnemyWordLabel>().SetWord(names[i]);
            }
            yield return Capture("after-level-1-1-grass-1080x1920");
            foreach(var sample in samples)Destroy(sample);samples.Clear();
            yield return SceneManager.UnloadSceneAsync("Game");SelectedLevelContext.Set(1,102);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity",LoadSceneMode.Additive);SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
            yield return null;yield return null;
            foreach(var spawner in FindObjectsOfType<SpawnerWaves>()){spawner.StopAllCoroutines();spawner.enabled=false;}
            foreach(var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()){outcome.StopAllCoroutines();outcome.enabled=false;}
            yield return null;ValidateMiniMap("1-2");
            yield return Capture("after-level-1-2-1080x1920");
        }
        Time.timeScale=1;
    }
    void ValidateMiniMap(string stage)
    {
        var mini=FindObjectOfType<MiniMapUI>();var type=typeof(MiniMapUI);var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var camera=(Camera)type.GetField("miniCamera",flags).GetValue(mini);var ground=BattleMapLoader.Instance.GroundTilemap;
        Record(stage+" terrain layer is included by minimap camera",(camera.cullingMask & (1<<ground.gameObject.layer))!=0);
        var view=(UnityEngine.UI.RawImage)type.GetField("mapImage",flags).GetValue(mini);var rt=view.texture as RenderTexture;
        var previous=RenderTexture.active;var pixels=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();
            var colors=pixels.GetPixels32();Color clear=camera.backgroundColor;int painted=colors.Count(c=>Mathf.Abs(c.r/255f-clear.r)+Mathf.Abs(c.g/255f-clear.g)+Mathf.Abs(c.b/255f-clear.b)>.12f);
            Record(stage+" minimap contains rendered terrain rather than only black background",painted>colors.Length/2,"Painted terrain pixels: "+painted+" / "+colors.Length);
        }
        finally{RenderTexture.active=previous;Destroy(pixels);}
    }
    static Vector3 World(Vector2 viewport){var p=Camera.main.ViewportToWorldPoint(new Vector3(viewport.x,viewport.y,10));p.z=0;return p;}
    static float Contrast(Color a,Color b){a=a.linear;b=b.linear;float x=.2126f*a.r+.7152f*a.g+.0722f*a.b,y=.2126f*b.r+.7152f*b.g+.0722f*b.b;return (Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);}
}
#endif
