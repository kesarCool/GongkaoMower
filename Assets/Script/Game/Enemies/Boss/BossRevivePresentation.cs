using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Reuses actual glyph quads for scatter / return; a detached root survives body hiding and never scales physics.</summary>
[DefaultExecutionOrder(350)]
[DisallowMultipleComponent]
public sealed class BossRevivePresentation:MonoBehaviour,IPoolReceiver
{
    [SerializeField] Material solidMaterial;
    [SerializeField] int sortingOrder=5,maxGlyphs=32;
    [SerializeField,Range(1f,3f)] float scatterDistanceMultiplier=1.6f;
    sealed class GlyphBatch
    {
        public GameObject go;public Mesh mesh;public MeshRenderer renderer;
        public readonly List<Vector3> source=new List<Vector3>(128),animated=new List<Vector3>(128),centers=new List<Vector3>(32);
        public readonly List<Vector2> uv=new List<Vector2>(128),uv2=new List<Vector2>(128);
        public readonly List<Color32> original=new List<Color32>(128),colors=new List<Color32>(128);
        public readonly List<int> triangles=new List<int>(192),indices=new List<int>(32);
        public void Clear(){source.Clear();animated.Clear();centers.Clear();uv.Clear();uv2.Clear();original.Clear();colors.Clear();triangles.Clear();indices.Clear();}
    }
    readonly List<GlyphBatch> batches=new List<GlyphBatch>(2);
    readonly List<Vector3> vertices=new List<Vector3>(2200);
    readonly List<Vector2> uv=new List<Vector2>(2200);
    readonly List<Color32> colors=new List<Color32>(2200);
    readonly List<int> triangles=new List<int>(3300);
    EnemyBase enemy;EnemyWordLabel label;TextMeshPro text;
    GameObject runtimeRoot;Mesh mesh;MeshRenderer meshRenderer;ResistShield shield;
    Vector3 center,bodyOrigin;float halfWidth=.8f,progress,phaseAt,pulseAt=-10;int phase;bool hidesText,hadVisibleText;
    public int Phase=>phase; // 1 scatter / gather, 2 body recovery, 3 actual shield, 4 dissipating sparks.
    public int GlyphCount {get;private set;}
    public float WaitProgress=>progress;
    public bool HasVisibleGeometry=>runtimeRoot!=null&&runtimeRoot.activeSelf&&meshRenderer!=null&&meshRenderer.enabled&&vertices.Count>0;
    public int RuntimeMeshCount=>mesh==null?0:1+batches.Count;
    public Transform RuntimeRoot=>runtimeRoot!=null?runtimeRoot.transform:null;
    public int ActiveGlyphRendererCount {get {int n=0;foreach(var b in batches)if(b.go!=null&&b.go.activeInHierarchy)n++;return n;}}
    public Vector3 CorePosition=>center;
    public float ReturnError {get;private set;}
    public bool ProtectionVisible=>phase==3&&shield!=null&&shield.IsActive;
    void Awake(){enemy=GetComponentInParent<EnemyBase>();label=enemy!=null?enemy.GetComponentInChildren<EnemyWordLabel>(true):null;}
    void OnEnable(){EventBus.Subscribe<DamageResistedEvent>(OnResisted,owner:this);}
    void OnResisted(DamageResistedEvent e){if(e.enemy==enemy&&phase==3)pulseAt=Time.time;}
    static void Setup(MeshRenderer r,int order)
    {r.sortingOrder=order;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;}
    void EnsureRuntime()
    {
        if(runtimeRoot!=null)return;
        ReleaseMeshes();
        runtimeRoot=new GameObject("BossReviveRuntime");runtimeRoot.hideFlags=HideFlags.DontSave;
        var ground=new GameObject("Core",typeof(MeshFilter),typeof(MeshRenderer));ground.transform.SetParent(runtimeRoot.transform,false);
        mesh=new Mesh{name="boss_revive_core",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();ground.GetComponent<MeshFilter>().sharedMesh=mesh;
        meshRenderer=ground.GetComponent<MeshRenderer>();meshRenderer.sharedMaterial=solidMaterial;Setup(meshRenderer,sortingOrder);meshRenderer.enabled=false;
    }
    GlyphBatch Batch(int index,Material material,int order)
    {
        while(batches.Count<=index)
        {
            var go=new GameObject("Glyphs_"+batches.Count,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(runtimeRoot.transform,false);
            var copy=new Mesh{name="boss_revive_glyphs",hideFlags=HideFlags.DontSave};copy.MarkDynamic();go.GetComponent<MeshFilter>().sharedMesh=copy;
            batches.Add(new GlyphBatch{go=go,mesh=copy,renderer=go.GetComponent<MeshRenderer>()});
        }
        var batch=batches[index];batch.renderer.sharedMaterial=material;Setup(batch.renderer,order);batch.Clear();return batch;
    }
    void RestoreText(){if(hidesText&&text!=null)text.renderer.enabled=true;hidesText=false;}
    public void BeginDeath()
    {
        if(!isActiveAndEnabled)return;RestoreText();Cancel();EnsureRuntime();runtimeRoot.SetActive(true);phase=1;progress=0;phaseAt=Time.time;GlyphCount=0;ReturnError=0;
        bodyOrigin=enemy.transform.position;Vector2 visual;center=label!=null&&label.TryGetVisualCenter(out visual)?new Vector3(visual.x,visual.y,bodyOrigin.z):bodyOrigin;
        halfWidth=.85f;
        foreach(var b in batches){b.Clear();b.go.SetActive(false);}
        text=null;hadVisibleText=label!=null&&label.TryGetWorldTextForShatter(out text)&&text.isActiveAndEnabled&&text.renderer.enabled;
        if(hadVisibleText)
        {
            label.PrepareForReviveSnapshot();text.ForceMeshUpdate();var info=text.textInfo;int used=0;
            for(int material=0;material<info.materialCount;material++)
            {
                var data=info.meshInfo[material];GlyphBatch batch=null;
                for(int c=0;c<info.characterCount&&GlyphCount<maxGlyphs;c++)
                {
                    var ch=info.characterInfo[c];if(!ch.isVisible||ch.materialReferenceIndex!=material)continue;
                    if(batch==null)batch=Batch(used++,data.material!=null?data.material:text.fontSharedMaterial,text.renderer.sortingOrder);
                    int v=batch.source.Count;Vector3 middle=Vector3.zero;
                    for(int k=0;k<4;k++)
                    {
                        int source=ch.vertexIndex+k;Vector3 p=text.transform.TransformPoint(data.vertices[source]);
                        batch.source.Add(p);batch.animated.Add(p);middle+=p;
                        batch.uv.Add(data.uvs0[source]);batch.uv2.Add(data.uvs2[source]);batch.original.Add(data.colors32[source]);batch.colors.Add(data.colors32[source]);
                    }
                    batch.centers.Add(middle*.25f);batch.indices.Add(GlyphCount++);
                    batch.triangles.Add(v);batch.triangles.Add(v+1);batch.triangles.Add(v+2);batch.triangles.Add(v+2);batch.triangles.Add(v+3);batch.triangles.Add(v);
                }
                if(batch!=null){batch.mesh.Clear();batch.mesh.SetVertices(batch.animated);batch.mesh.SetUVs(0,batch.uv);batch.mesh.SetUVs(1,batch.uv2);batch.mesh.SetColors(batch.colors);batch.mesh.SetTriangles(batch.triangles,0);batch.mesh.RecalculateBounds();batch.go.SetActive(true);}
            }
            halfWidth=Mathf.Clamp(text.renderer.bounds.extents.x+.22f,.85f,4);
            var bounds=text.renderer.bounds;center=new Vector3(bounds.center.x,bounds.center.y,bodyOrigin.z);
        }
        RenderNow();
    }
    public void SetWaitProgress(float value){if(phase==1)progress=Mathf.Clamp01(value);}
    public void BeginRecovery()
    {if(!isActiveAndEnabled)return;EnsureRuntime();phase=2;phaseAt=Time.time;progress=1;if(hadVisibleText&&text!=null){text.renderer.enabled=false;hidesText=true;}RenderNow();}
    public void SetRecoveryProgress(float value){if(phase==2)progress=Mathf.Clamp01(value);}
    public void FinishRecovery(){RestoreText();foreach(var b in batches)if(b.go!=null)b.go.SetActive(false);}
    public void BindProtection(ResistShield activeShield)
    {FinishRecovery();if(!isActiveAndEnabled)return;EnsureRuntime();shield=activeShield;phase=3;phaseAt=Time.time;RenderNow();}
    void LateUpdate(){RenderNow();}
    public void RenderNow()
    {
        if(phase==0||runtimeRoot==null)return;
        if(enemy==null||!enemy.gameObject.activeInHierarchy){Cancel();return;}
        if(phase==3&&(shield==null||!shield.IsActive)){phase=4;phaseAt=Time.time;}
        Vector3 anchor=center;if(phase>=2){Vector2 p;anchor=label!=null&&label.TryGetVisualCenter(out p)?new Vector3(p.x,p.y,enemy.transform.position.z):center+enemy.transform.position-bodyOrigin;}
        AnimateGlyphs();vertices.Clear();colors.Clear();uv.Clear();triangles.Clear();Vector3 foot=anchor+Vector3.down*.65f;
        float elapsed=Time.time-phaseAt;
        if(phase==1)
        {
            float scatter=Mathf.Clamp01(progress/.25f),gather=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.46f,.96f,progress));
            Arc(foot,1.28f,.4f,.09f,0,360,new Color(.29f,.025f,.012f,.95f),48);
            Arc(foot,1.28f,.4f,.035f,-90,-90+progress*360,new Color(1,.46f,.08f,.95f),48);
            for(int i=0;i<4;i++)Arc(foot,.98f,.3f,.035f,i*90+elapsed*90,i*90+50+elapsed*90,new Color(1,.2f,.035f,.85f),8);
            if(progress<.25f)Arc(foot,.6f+scatter*1.5f,.2f+scatter*.5f,.065f,0,360,new Color(1,.22f,.035f,1-scatter),40);
            float radius=.16f+Mathf.Sin(progress*Mathf.PI)*.13f;
            Diamond(anchor,radius+.075f,new Color(.34f,.025f,.008f,.95f),true);
            Diamond(anchor,radius,new Color(1,.32f,.035f,.95f),true);Diamond(anchor,radius*.4f,new Color(1,.87f,.4f,1),true);
            for(int i=0;i<8;i++)
            {float a=(i*45+elapsed*50)*Mathf.Deg2Rad;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.7f,0);float r=Mathf.Lerp(1.45f,.4f,gather);Segment(anchor+d*r,anchor+d*(r+.17f),.04f,new Color(1,.56f,.13f,.7f));}
        }
        else if(phase==2)
        {
            Arc(foot,1.2f+progress*.85f,.4f+progress*.27f,.065f,0,360,new Color(1,.51f,.1f,1-progress*.65f),48);
            for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Diamond(anchor+new Vector3(Mathf.Cos(a)*(1+progress*.65f),Mathf.Sin(a)*(.5f+progress*.25f),0),.09f,new Color(1,.62f,.15f,1-progress),true);}
        }
        else if(phase==3)
        {
            float flash=Mathf.Clamp01(1-(Time.time-pulseAt)/.12f);Color rim=Color.Lerp(new Color(1,.62f,.13f,.95f),new Color(1,.94f,.67f,1),flash);
            for(int side=0;side<2;side++){float from=side==0?-65:115;Arc(anchor,halfWidth,.83f,.10f,from,from+130,new Color(.36f,.055f,.015f,.9f),28);Arc(anchor,halfWidth,.83f,.035f+flash*.025f,from,from+130,rim,28);}
            Arc(foot,1.18f,.36f,.035f,0,360,new Color(1,.62f,.13f,.6f),40);
        }
        else if(phase==4)
        {
            float p=Mathf.Clamp01(elapsed/.16f);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.6f,0);Segment(anchor+d*(.9f+p*.45f),anchor+d*(1.06f+p*.5f),.035f,new Color(1,.62f,.14f,1-p));}
            if(p>=1){Cancel();return;}
        }
        mesh.Clear(false);mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();meshRenderer.enabled=vertices.Count>0&&solidMaterial!=null;
    }
    void AnimateGlyphs()
    {
        ReturnError=0;if(phase!=1&&phase!=2)return;
        float burst=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.23f,progress)),gather=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.46f,.96f,progress));
        float amount=phase==1?burst*(1-gather):0,bounce=phase==2?1+.12f*Mathf.Sin(progress*Mathf.PI):1;
        Vector3 shift=phase==2?enemy.transform.position-bodyOrigin:Vector3.zero;
        foreach(var b in batches)
        {
            if(b.source.Count==0||b.go==null)continue;b.go.SetActive(true);
            for(int i=0;i<b.centers.Count;i++)
            {
                int index=b.indices[i];Vector3 c=b.centers[i];float sign=index%2==0?-1:1;
                var offset=new Vector3((c.x-center.x)*.5f,.8f+.22f*(index%3),0)*(amount*scatterDistanceMultiplier);
                float rotation=sign*(22+index%3*9)*amount*Mathf.Deg2Rad,cos=Mathf.Cos(rotation),sin=Mathf.Sin(rotation),scale=1-.18f*amount;
                for(int k=0;k<4;k++)
                {
                    int v=i*4+k;Vector3 p=(b.source[v]-c)*scale;var rotated=new Vector3(p.x*cos-p.y*sin,p.x*sin+p.y*cos,p.z);
                    var point=c+offset+rotated;point=center+(point-center)*bounce+shift;point.z-=.12f;b.animated[v]=point;
                    var color=b.original[v];color.a=(byte)(color.a*(1-.24f*amount));b.colors[v]=color;
                    if(phase==1&&progress>=.96f)ReturnError=Mathf.Max(ReturnError,Vector2.Distance(point,b.source[v]));
                }
            }
            b.mesh.SetVertices(b.animated);b.mesh.SetColors(b.colors);b.mesh.RecalculateBounds();
        }
    }
    void Vertex(Vector3 p,Color c){p.z-=.17f;vertices.Add(p);colors.Add(c);uv.Add(Vector2.zero);}
    void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){int i=vertices.Count;Vertex(a,color);Vertex(b,color);Vertex(c,color);Vertex(d,color);triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i);triangles.Add(i+2);triangles.Add(i+3);}
    void Segment(Vector3 a,Vector3 b,float width,Color c){var d=(b-a).normalized;var n=new Vector3(-d.y,d.x,0)*width*.5f;Quad(a-n,b-n,b+n,a+n,c);}
    void Arc(Vector3 c,float x,float y,float width,float from,float to,Color color,int segments){for(int i=0;i<segments;i++){float a=Mathf.Lerp(from,to,(float)i/segments)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(float)(i+1)/segments)*Mathf.Deg2Rad;Segment(c+new Vector3(Mathf.Cos(a)*x,Mathf.Sin(a)*y,0),c+new Vector3(Mathf.Cos(b)*x,Mathf.Sin(b)*y,0),width,color);}}
    void Diamond(Vector3 c,float r,Color color,bool fill){var l=c+Vector3.left*r;var d=c+Vector3.down*r;var ri=c+Vector3.right*r;var u=c+Vector3.up*r;if(fill)Quad(l,d,ri,u,color);else{Segment(l,d,.025f,color);Segment(d,ri,.025f,color);Segment(ri,u,.025f,color);Segment(u,l,.025f,color);}}
    public void Cancel(){RestoreText();phase=0;shield=null;vertices.Clear();if(mesh!=null)mesh.Clear();if(runtimeRoot!=null)runtimeRoot.SetActive(false);}
    void ReleaseMeshes(){if(mesh!=null)Destroy(mesh);mesh=null;foreach(var b in batches)if(b.mesh!=null)Destroy(b.mesh);batches.Clear();}
    public void OnPoolGet(){Cancel();if(enemy!=null)enemy.ShowFromRevive();}
    public void OnPoolRelease(){Cancel();}
    void OnDisable(){EventBus.Unsubscribe<DamageResistedEvent>(OnResisted);Cancel();}
    void OnDestroy(){Cancel();ReleaseMeshes();if(runtimeRoot!=null)Destroy(runtimeRoot);}
}
