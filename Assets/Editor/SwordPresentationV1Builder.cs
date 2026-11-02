#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SwordPresentationV1Builder
{
    public const string RecipePath="Tools/VFX/Library/Projectiles/sword_presentation_v1.json";
    public const string AtlasPath="Assets/Res/Textures/VFX/sheet_sword_presentation_v1.png";
    public const string MaterialPath="Assets/Res/Materials/VFX/mat_sword_presentation_v1.mat";
    public const string TrailMaterialPath="Assets/Res/Materials/VFX/mat_sword_trail_v1.mat";
    public const string HitPath="Assets/Res/Prefabs/Effects/Impact/fx_sword_hit_v1.prefab";
    public const string DefinitionPath="Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_AutoProjectileSword.asset";
    public static readonly string[] ProjectilePaths={"Assets/Prefab/Combat/playerBullet/BulletStraightSword.prefab","Assets/Prefab/Combat/playerBullet/BulletStraightSwordMax.prefab","Assets/Prefab/Combat/playerBullet/BurstBulletSword.prefab"};
    const string Output="ArtOutput/VFX/SwordPresentationV1/Audit/";
    public static SwordVisualRecipe ReadRecipe()
    {
        var r=JsonConvert.DeserializeObject<SwordVisualRecipe>(File.ReadAllText(RecipePath),new JsonSerializerSettings {MissingMemberHandling=MissingMemberHandling.Error});
        if(r==null || r.schemaVersion!=1 || r.id!="sword_presentation_v1" || r.mode!="projectile-style" || r.texturePath!=AtlasPath || r.materialPath!=MaterialPath || r.trailMaterialPath!=TrailMaterialPath || r.hitPrefabPath!=HitPath || r.width!=512 || r.height!=256 || r.pixelsPerUnit!=256)
            throw new InvalidOperationException("Invalid sword visual recipe identity/layout.");
        if(!(r.pngBudgetKiB>=1 && r.pngBudgetKiB<=96 && r.normalScale>0 && r.maxScale>=r.normalScale && r.maxScale<=1.2f && r.normalBladeAspectY>=1 && r.normalBladeAspectY<=1.6f && r.maxBladeAspectY>=1 && r.maxBladeAspectY<=1.6f && r.normalTrailSeconds>0 && r.maxTrailSeconds>=r.normalTrailSeconds && r.maxTrailSeconds<=.12f && r.normalTrailWidth>0 && r.maxTrailWidth>=r.normalTrailWidth && r.maxTrailWidth<=.12f && r.hitSeconds>=.1f && r.hitSeconds<=.4f && r.poolSeconds>=r.hitSeconds+.04f && r.poolSeconds<=.5f && r.normalHitRadius>0 && r.maxHitRadius>=r.normalHitRadius && r.maxHitRadius<=.45f && r.spriteOrder<0 && r.trailOrder<r.spriteOrder && r.hitOrder<r.spriteOrder))
            throw new InvalidOperationException("Sword recipe exceeds opacity/timing/layer budget.");
        if(r.bladeLengthMultiplier<.6f || r.bladeLengthMultiplier>1f) throw new InvalidOperationException("Invalid sword blade length multiplier.");
        foreach(var p in new[]{r.bladeSource,r.hitSource}) if(p==null || !p.StartsWith("ArtOutput/VFX/SwordPresentationV1/Sources/") || p.Contains("..")) throw new InvalidOperationException("Invalid sword source path.");
        return r;
    }
    [MenuItem("工具/VFX/Build Sword Presentation V1")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var r=ReadRecipe(); Directory.CreateDirectory(Output);
        foreach(var p in new[]{AtlasPath,MaterialPath,TrailMaterialPath,HitPath}) Directory.CreateDirectory(Path.GetDirectoryName(p));
        Pack(r); AssetDatabase.ImportAsset(AtlasPath,ImportAssetOptions.ForceUpdate);
        var ti=(TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Multiple;ti.spritePixelsPerUnit=r.pixelsPerUnit;
        ti.sRGBTexture=true;ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.isReadable=false;
        ti.maxTextureSize=512;ti.npotScale=TextureImporterNPOTScale.None;ti.wrapMode=TextureWrapMode.Clamp;ti.filterMode=FilterMode.Bilinear;
        var settings=new TextureImporterSettings();ti.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;ti.SetTextureSettings(settings);
        ti.spritesheet=new[]{new SpriteMetaData {name="sword_qingfeng_v1",rect=new Rect(0,0,256,128),alignment=(int)SpriteAlignment.Center,pivot=new Vector2(.5f,.5f)},new SpriteMetaData {name="sword_double_edge_v1",rect=new Rect(256,0,256,128),alignment=(int)SpriteAlignment.Center,pivot=new Vector2(.5f,.5f)}};
        var web=ti.GetPlatformTextureSettings("WebGL");web.overridden=true;web.maxTextureSize=512;web.format=TextureImporterFormat.ETC2_RGBA8Crunched;web.crunchedCompression=true;web.compressionQuality=70;ti.SetPlatformTextureSettings(web);ti.SaveAndReimport();
        var material=GetMaterial(MaterialPath,"Sprites/Default");material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);material.color=Color.white;EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        var trailMaterial=GetMaterial(TrailMaterialPath,"GongkaoMower/TalismanTrailSoft");AssetDatabase.SaveAssetIfDirty(trailMaterial);
        var hit=BuildHit(r,material); var sprites=AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToArray();
        for(int i=0;i<ProjectilePaths.Length;i++) ConfigureProjectile(ProjectilePaths[i],sprites.Single(s=>s.name==(i==0?"sword_qingfeng_v1":"sword_double_edge_v1")),material,trailMaterial,hit,r,i!=0);
        Audit();
    }
    static Material GetMaterial(string path,string shaderName)
    {
        var shader=Shader.Find(shaderName);if(shader==null) throw new InvalidOperationException("Missing "+shaderName);
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null) {material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
        material.shader=shader;EditorUtility.SetDirty(material);return material;
    }
    static void Pack(SwordVisualRecipe r)
    {
        var atlas=new Texture2D(512,256,TextureFormat.RGBA32,false);var blades=new Texture2D(2,2,TextureFormat.RGBA32,false);var hits=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!blades.LoadImage(File.ReadAllBytes(r.bladeSource)) || !hits.LoadImage(File.ReadAllBytes(r.hitSource))) throw new InvalidOperationException("Missing generated sword sources.");
            atlas.SetPixels32(new Color32[512*256]); var pixels=blades.GetPixels32();
            // The generated rows need not divide the canvas exactly in half. Split at the largest empty horizontal gap.
            int best=0,split=blades.height/2,start=-1;
            for(int y=blades.height/4;y<blades.height*3/4;y++)
            {
                bool ink=false;for(int x=0;x<blades.width;x++) if(pixels[y*blades.width+x].a>16) {ink=true;break;}
                if(!ink && start<0) start=y;
                if((ink || y==blades.height*3/4-1) && start>=0) {int length=y-start;if(length>best) {best=length;split=start+length/2;}start=-1;}
            }
            if(best<4) throw new InvalidOperationException("Two sword source rows have no safe transparent separation.");
            var bounds=new RectInt[2];
            for(int state=0;state<2;state++)
            {
                int lo=state==0?split:0,hi=state==0?blades.height:split,minX=blades.width,minY=hi,maxX=-1,maxY=-1;
                for(int y=lo;y<hi;y++) for(int x=0;x<blades.width;x++) if(pixels[y*blades.width+x].a>16) {minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                if(maxX<minX || maxY<minY) throw new InvalidOperationException("Empty sword source row.");
                var b=new RectInt(Mathf.Max(0,minX-3),Mathf.Max(lo,minY-3),Mathf.Min(blades.width-1,maxX+3)-Mathf.Max(0,minX-3)+1,Mathf.Min(hi-1,maxY+3)-Mathf.Max(lo,minY-3)+1);bounds[state]=b;
                float scale=Mathf.Min(228f/b.width,112f/b.height);int w=Mathf.RoundToInt(b.width*scale),h=Mathf.RoundToInt(b.height*scale),dx=state*256+(256-w)/2,dy=(128-h)/2;
                for(int y=0;y<h;y++) for(int x=0;x<w;x++) atlas.SetPixel(dx+x,dy+y,blades.GetPixelBilinear((b.x+(x+.5f)/w*b.width)/blades.width,(b.y+(y+.5f)/h*b.height)/blades.height));
            }
            // Preserve the generated cell centres and relative expansion; only resample and pad each cell.
            for(int f=0;f<8;f++) for(int y=0;y<56;y++) for(int x=0;x<56;x++)
                atlas.SetPixel(f*64+4+x,196+y,hits.GetPixelBilinear((f%4+(x+.5f)/56)/4,(1-f/4+(y+.5f)/56)/2));
            atlas.Apply();byte[] png=atlas.EncodeToPNG();if(png.Length>r.pngBudgetKiB*1024) throw new InvalidOperationException("Sword atlas exceeds source PNG budget.");File.WriteAllBytes(AtlasPath,png);
            File.WriteAllText(Output+"packing.json",JsonConvert.SerializeObject(new {splitY=split,normalBounds=bounds[0].ToString(),maxBounds=bounds[1].ToString(),runtimeWidth=512,runtimeHeight=256,hitFrames=8,sourceCellOrder="4x2 top-left first",pngBytes=png.Length},Formatting.Indented));
        }
        finally {UnityEngine.Object.DestroyImmediate(atlas);UnityEngine.Object.DestroyImmediate(blades);UnityEngine.Object.DestroyImmediate(hits);}
    }
    static GameObject BuildHit(SwordVisualRecipe r,Material material)
    {
        var root=new GameObject("fx_sword_hit_v1");
        try
        {
            var ps=root.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.duration=r.hitSeconds;main.startLifetime=r.hitSeconds;main.startSpeed=0;main.startSize=.95f;main.startColor=Color.white;main.maxParticles=1;main.loop=false;main.playOnAwake=false;main.useUnscaledTime=false;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            ps.useAutoRandomSeed=false;ps.randomSeed=8721;var shape=ps.shape;shape.enabled=false;var emission=ps.emission;emission.rateOverTime=0;emission.rateOverDistance=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,1)});
            var sheet=ps.textureSheetAnimation;sheet.enabled=true;sheet.mode=ParticleSystemAnimationMode.Grid;sheet.numTilesX=8;sheet.numTilesY=4;sheet.animation=ParticleSystemAnimationType.WholeSheet;sheet.timeMode=ParticleSystemAnimationTimeMode.Lifetime;sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,7.999f/32));sheet.startFrame=0;sheet.cycleCount=1;
            var alpha=new Gradient();alpha.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.3f),new GradientAlphaKey(0,1)});var color=ps.colorOverLifetime;color.enabled=true;color.color=alpha;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=r.hitOrder;renderer.maxParticleSize=1;
            var pool=root.AddComponent<PooledCombatVfx>();var so=new SerializedObject(pool);so.FindProperty("_particles").arraySize=1;so.FindProperty("_particles").GetArrayElementAtIndex(0).objectReferenceValue=ps;so.FindProperty("lifetimeOverride").floatValue=r.poolSeconds;so.FindProperty("sortingOrder").intValue=r.hitOrder;so.FindProperty("referenceRadius").floatValue=r.normalHitRadius;so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root,HitPath);
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }
    static void ConfigureProjectile(string path,Sprite sprite,Material material,Material trailMaterial,GameObject hit,SwordVisualRecipe r,bool upgraded)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            float scale=upgraded?r.maxScale:r.normalScale,aspectY=upgraded?r.maxBladeAspectY:r.normalBladeAspectY;
            foreach(var sr in root.GetComponentsInChildren<SpriteRenderer>(true)) {sr.sprite=sprite;sr.sharedMaterial=material;sr.color=Color.white;sr.sortingOrder=r.spriteOrder;sr.transform.localRotation=Quaternion.identity;sr.transform.localScale=new Vector3(scale*r.bladeLengthMultiplier,scale*aspectY,1);}
            var trailRoot=root.transform.Find("SwordWake");if(trailRoot==null) {var go=new GameObject("SwordWake");trailRoot=go.transform;trailRoot.SetParent(root.transform,false);}
            var trail=trailRoot.GetComponent<TrailRenderer>();
            foreach(var old in root.GetComponentsInChildren<TrailRenderer>(true)) if(old!=trail) UnityEngine.Object.DestroyImmediate(old);
            if(trail==null) trail=trailRoot.gameObject.AddComponent<TrailRenderer>();
            trailRoot.localPosition=new Vector3(-.18f,0,.02f);trail.sharedMaterial=trailMaterial;trail.time=upgraded?r.maxTrailSeconds:r.normalTrailSeconds;trail.widthMultiplier=upgraded?r.maxTrailWidth:r.normalTrailWidth;
            trail.widthCurve=new AnimationCurve(new Keyframe(0,1),new Keyframe(.4f,.7f),new Keyframe(1,0));
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(upgraded?new Color(1,.78f,.25f):new Color(.15f,.88f,1),0),new GradientColorKey(new Color(.04f,.30f,.65f),.45f),new GradientColorKey(new Color(.025f,.065f,.20f),1)},new[]{new GradientAlphaKey(.85f,0),new GradientAlphaKey(.40f,.5f),new GradientAlphaKey(0,1)});trail.colorGradient=gradient;
            trail.minVertexDistance=.06f;trail.numCornerVertices=1;trail.numCapVertices=2;trail.alignment=LineAlignment.View;trail.textureMode=LineTextureMode.Stretch;trail.autodestruct=false;trail.emitting=true;trail.sortingOrder=r.trailOrder;trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;trail.generateLightingData=false;
            var feedback=root.GetComponent<ProjectileHitFeedback>();if(feedback==null) feedback=root.AddComponent<ProjectileHitFeedback>();var so=new SerializedObject(feedback);so.FindProperty("hitPrefab").objectReferenceValue=hit;so.FindProperty("visualRadius").floatValue=upgraded?r.maxHitRadius:r.normalHitRadius;so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    [MenuItem("工具/VFX/检查刀光剑影配置")]
    public static void Audit()
    {
        var r=ReadRecipe();var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);var ti=(TextureImporter)AssetImporter.GetAtPath(AtlasPath);var prefabs=ProjectilePaths.Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p)).ToArray();var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);var trailMaterial=AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);var hit=AssetDatabase.LoadAssetAtPath<GameObject>(HitPath);
        bool recipeMatches=true;
        for(int i=0;i<prefabs.Length;i++)
        {
            bool upgraded=i!=0;var sr=prefabs[i].GetComponentInChildren<SpriteRenderer>();var trail=prefabs[i].GetComponentInChildren<TrailRenderer>();var feedback=new SerializedObject(prefabs[i].GetComponent<ProjectileHitFeedback>());
            float scale=upgraded?r.maxScale:r.normalScale,aspectY=upgraded?r.maxBladeAspectY:r.normalBladeAspectY;
            recipeMatches &= sr.sprite.name==(upgraded?"sword_double_edge_v1":"sword_qingfeng_v1") && sr.transform.localScale==new Vector3(scale*r.bladeLengthMultiplier,scale*aspectY,1) && trail.time==(upgraded?r.maxTrailSeconds:r.normalTrailSeconds) && trail.widthMultiplier==(upgraded?r.maxTrailWidth:r.normalTrailWidth) && trail.sortingOrder==r.trailOrder && feedback.FindProperty("hitPrefab").objectReferenceValue==hit && feedback.FindProperty("visualRadius").floatValue==(upgraded?r.maxHitRadius:r.normalHitRadius);
        }
        var hitMain=hit.GetComponent<ParticleSystem>().main;var hitPool=new SerializedObject(hit.GetComponent<PooledCombatVfx>());
        bool passed=texture!=null && texture.width==512 && texture.height==256 && !ti.isReadable && !ti.mipmapEnabled && new FileInfo(AtlasPath).Length<=r.pngBudgetKiB*1024 && ti.GetPlatformTextureSettings("WebGL").format==TextureImporterFormat.ETC2_RGBA8Crunched &&
            prefabs.All(p=>p.GetComponent<ProjectileHitFeedback>()!=null && p.GetComponentsInChildren<SpriteRenderer>().Length==1 && p.GetComponentInChildren<SpriteRenderer>().sharedMaterial==material && p.GetComponentInChildren<SpriteRenderer>().sprite.texture==texture && p.GetComponentInChildren<SpriteRenderer>().sortingOrder==r.spriteOrder && p.GetComponentsInChildren<TrailRenderer>().Length==1 && p.GetComponentInChildren<TrailRenderer>().sharedMaterial==trailMaterial && p.GetComponentInChildren<TrailRenderer>().time<=.12f) && hit.GetComponentsInChildren<ParticleSystem>().Length==1 && recipeMatches && hitMain.duration==r.hitSeconds && hitMain.startLifetime.constant==r.hitSeconds && hitPool.FindProperty("lifetimeOverride").floatValue==r.poolSeconds && hitPool.FindProperty("referenceRadius").floatValue==r.normalHitRadius;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"configuration.json",JsonConvert.SerializeObject(new {passed,recipe=JObject.Parse(File.ReadAllText(RecipePath)),checkedUtc=DateTime.UtcNow},Formatting.Indented));
        File.WriteAllText(Output+"resources.json",JsonConvert.SerializeObject(new {sourcePngBytes=new FileInfo(AtlasPath).Length,texture.width,texture.height,format=texture.format.ToString(),etc2BlockBytesWithoutMipmaps=512*256,editorTextureMemoryBytes=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture),sharedMaterials=2,addedTrailTextures=0,renderersPerProjectile=2,hitLayers=1,hitFrames=8,hitSeconds=r.hitSeconds,poolSeconds=r.poolSeconds,projectilePaths=ProjectilePaths,note="Source PNG bytes, ETC2 block data and Editor API memory are not WeChat upload size/device performance."},Formatting.Indented));
        if(!passed) throw new InvalidOperationException("Sword visual configuration drift.");
    }
}
[Serializable] public sealed class SwordVisualRecipe
{
    public int schemaVersion,width,height,pixelsPerUnit,pngBudgetKiB,spriteOrder,trailOrder,hitOrder;
    public string id,mode,displayName,status,texturePath,materialPath,trailMaterialPath,hitPrefabPath,bladeSource,hitSource,briefPath,promptPath,validationReport;
    public float normalScale,maxScale,bladeLengthMultiplier,normalBladeAspectY,maxBladeAspectY,normalTrailSeconds,maxTrailSeconds,normalTrailWidth,maxTrailWidth,hitSeconds,poolSeconds,normalHitRadius,maxHitRadius;
}
#endif
