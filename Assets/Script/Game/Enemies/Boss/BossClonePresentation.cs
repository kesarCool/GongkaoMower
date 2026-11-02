using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Identity, charge echoes and actual clone birth/expiry. Owns no skills, stats or collision.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
[DefaultExecutionOrder(250)]
public sealed class BossClonePresentation : MonoBehaviour,IPoolReceiver
{
    [SerializeField] Material solidMaterial;
    [SerializeField] GameObject exitPrefab;
    [SerializeField] int sortingOrder=4;
    [SerializeField] float cloneAlpha=.64f,birthSeconds=.42f,expiryFadeSeconds=.32f;
    readonly List<Vector3> vertices=new List<Vector3>(600);
    readonly List<Color32> colors=new List<Color32>(600);
    readonly List<Vector2> uv=new List<Vector2>(600);
    readonly List<int> triangles=new List<int>(900);
    sealed class Echo { public GameObject go;public Mesh mesh;public MeshRenderer renderer;public Color32[] original,faded;public int side; }
    readonly List<Echo> echoes=new List<Echo>();
    Mesh mesh;MeshRenderer meshRenderer;EnemyBase enemy;EnemyWordLabel label;CloneMarker marker;
    SpriteRenderer[] sprites;TMP_Text[] texts;
    bool ownerBadge,hasHeadCrest;int phase;float phaseAt,chargeSeconds;
    Matrix4x4 matrix;
    public int Phase=>phase;
    public bool IsClone=>marker!=null&&marker.IsConfigured;
    public bool HasVisibleGeometry=>meshRenderer!=null&&meshRenderer.enabled&&vertices.Count>0;
    public int EchoRendererCount {get {int n=0;foreach(var echo in echoes)if(echo.go.activeSelf)n++;return n;}}
    public int RuntimeMeshCount=>mesh!=null?1+echoes.Count:0;
    public Vector2 VisualCenter {get {Vector2 p;return label!=null&&label.TryGetVisualCenter(out p)?p:enemy!=null?(Vector2)enemy.transform.position:(Vector2)transform.position;}}
    void Awake(){EnsureResources();}
    void EnsureResources()
    {
        if(mesh!=null)return;
        enemy=GetComponentInParent<EnemyBase>();label=enemy!=null?enemy.GetComponentInChildren<EnemyWordLabel>(true):null;
        hasHeadCrest=enemy!=null&&enemy.GetComponentInChildren<BossHeadCrestPresentation>(true)!=null;
        marker=GetComponentInParent<CloneMarker>();sprites=enemy!=null?enemy.GetComponentsInChildren<SpriteRenderer>(true):new SpriteRenderer[0];texts=enemy!=null?enemy.GetComponentsInChildren<TMP_Text>(true):new TMP_Text[0];
        // Instantiate may copy an owner's inactive, runtime-only charge children. They do
        // not own the source meshes and must not become permanent renderer copies on clones.
        foreach(Transform child in transform)if(child.name.StartsWith("CloneChargeEcho_")){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        mesh=new Mesh{name="boss_clone_identity",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;
        meshRenderer=GetComponent<MeshRenderer>();meshRenderer.sharedMaterial=solidMaterial;SetupRenderer(meshRenderer,sortingOrder);meshRenderer.enabled=false;
    }
    static void SetupRenderer(MeshRenderer r,int order){r.sortingOrder=order;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;}
    public void EnableOwnerBadge(){EnsureResources();ownerBadge=true;}
    public void DisableOwnerBadge(){ownerBadge=false;CancelCharge();}
    public void ConfigureClone(CloneMarker identity){EnsureResources();marker=identity;ownerBadge=false;CancelCharge();ApplyCloneAlpha();RenderNow();}
    public void BeginCharge(float seconds)
    {
        EnsureResources();phase=1;phaseAt=Time.time;chargeSeconds=Mathf.Max(.01f,seconds);BuildEchoes();RenderNow();
    }
    public void ReleaseCharge(){phase=2;phaseAt=Time.time;HideEchoes();RenderNow();}
    public void CancelCharge(){phase=0;HideEchoes();}
    void HideEchoes(){foreach(var echo in echoes)if(echo.go!=null)echo.go.SetActive(false);}
    void BuildEchoes()
    {
        TextMeshPro text;if(label==null||!label.TryGetWorldTextForShatter(out text))return;
        text.ForceMeshUpdate();int used=0;var info=text.textInfo;
        for(int material=0;material<info.materialCount;material++)
        {
            var source=info.meshInfo[material];if(source.vertexCount==0)continue;
            for(int side=-1;side<=1;side+=2)
            {
                Echo echo;
                if(used<echoes.Count)echo=echoes[used];
                else
                {
                    var go=new GameObject("CloneChargeEcho_"+used,typeof(MeshFilter),typeof(MeshRenderer));go.hideFlags=HideFlags.DontSave;go.transform.SetParent(transform,false);
                    var copy=new Mesh{name="boss_clone_charge_glyph",hideFlags=HideFlags.DontSave};copy.MarkDynamic();go.GetComponent<MeshFilter>().sharedMesh=copy;
                    echo=new Echo{go=go,mesh=copy,renderer=go.GetComponent<MeshRenderer>()};echoes.Add(echo);
                }
                echo.side=side;var points=new Vector3[source.vertices.Length];for(int i=0;i<points.Length;i++)points[i]=transform.InverseTransformPoint(text.transform.TransformPoint(source.vertices[i]));
                echo.mesh.Clear();echo.mesh.vertices=points;echo.mesh.uv=source.uvs0;echo.mesh.uv2=source.uvs2;echo.mesh.triangles=source.triangles;
                echo.original=(Color32[])source.colors32.Clone();echo.faded=new Color32[echo.original.Length];echo.mesh.colors32=echo.original;echo.mesh.RecalculateBounds();
                echo.renderer.sharedMaterial=source.material!=null?source.material:text.fontSharedMaterial;SetupRenderer(echo.renderer,sortingOrder-1);echo.go.SetActive(true);used++;
            }
        }
        for(int i=used;i<echoes.Count;i++)echoes[i].go.SetActive(false);
    }
    void AnimateEchoes(float progress)
    {
        float offset=Mathf.Lerp(.08f,.36f,progress),opacity=Mathf.Sin(progress*Mathf.PI)*.23f;
        foreach(var echo in echoes)
        {
            if(!echo.go.activeSelf)continue;echo.go.transform.localPosition=new Vector3(echo.side*offset,echo.side*.035f,0);
            for(int i=0;i<echo.faded.Length;i++){var c=echo.original[i];c.a=(byte)(c.a*opacity);echo.faded[i]=c;}echo.mesh.colors32=echo.faded;
        }
    }
    void ApplyCloneAlpha()
    {
        if(!IsClone)return;float age=Time.time-marker.BornAt;
        float alpha=cloneAlpha*Mathf.Lerp(.6f,1,Mathf.Clamp01(age/birthSeconds));
        if(marker.RemainingSeconds<expiryFadeSeconds)alpha*=Mathf.Lerp(.55f,1,marker.RemainingSeconds/expiryFadeSeconds);
        foreach(var text in texts)if(text!=null){var c=text.color;if(Mathf.Abs(c.a-alpha)>.001f){c.a=alpha;text.color=c;}}
        foreach(var sprite in sprites)if(sprite!=null){var c=sprite.color;c.a=alpha;sprite.color=c;}
    }
    public void PlayExitFeedback()
    {
        if(exitPrefab!=null&&Application.isPlaying)CombatVfxSpawner.TryPlayPooled(exitPrefab,new Vector3(VisualCenter.x,VisualCenter.y,-.15f),Quaternion.identity,.55f);
    }
    void LateUpdate()
    {
        if(enemy==null||enemy.Hp<=0){Clear();HideEchoes();return;}
        ApplyCloneAlpha();if(phase==2&&Time.time-phaseAt>.28f)phase=0;RenderNow();
    }
    public void RenderNow()
    {
        EnsureResources();if(!ownerBadge&&!IsClone&&phase==0){Clear();return;}
        vertices.Clear();colors.Clear();uv.Clear();triangles.Clear();matrix=transform.worldToLocalMatrix;Vector2 center=VisualCenter,foot=center+Vector2.down*.64f;
        Color dark=IsClone?new Color(.52f,.035f,.035f,.85f):new Color(.46f,.19f,.025f,.95f);
        Color bright=IsClone?new Color(1,.25f,.12f,.8f):new Color(1,.7f,.22f,.95f);
        if(IsClone)
        {
            for(int i=0;i<8;i++){Arc(foot,.72f,.28f,.035f,i*45+5,i*45+29,dark,4);Arc(foot,.64f,.24f,.02f,i*45+7,i*45+27,bright,4);}
            if(!hasHeadCrest){Diamond(center+Vector2.up*.78f+Vector2.left*.1f,.10f,bright,false);Diamond(center+Vector2.up*.78f+Vector2.right*.1f,.10f,bright,false);}
            float age=Time.time-marker.BornAt;
            if(age<birthSeconds){float t=age/birthSeconds;Arc(foot,Mathf.Lerp(.3f,1.22f,t),Mathf.Lerp(.18f,.46f,t),.045f,0,360,new Color(.9f,.12f,.04f,(1-t)*.85f),40);}
            if(marker.RemainingSeconds<expiryFadeSeconds)
            {
                float t=1-marker.RemainingSeconds/expiryFadeSeconds;
                for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Segment(center+d*(.55f+t*.55f),center+d*(.68f+t*.65f),.035f,new Color(1,.35f,.1f,1-t*.6f));}
            }
        }
        else if(ownerBadge)
        {
            Arc(foot,.72f,.28f,.04f,0,360,dark,40);Arc(foot,.64f,.24f,.025f,0,360,bright,40);if(!hasHeadCrest)Diamond(center+Vector2.up*.78f,.12f,bright,true);
        }
        if(phase==1)
        {
            float t=Mathf.Clamp01((Time.time-phaseAt)/chargeSeconds);AnimateEchoes(t);
            Arc(foot,1.05f,.42f,.04f,-90,-90+360*t,new Color(1,.5f,.14f,.85f),40);
            Diamond(center+Vector2.left*(.95f+.3f*t),.16f,new Color(1,.4f,.16f,.75f),false);Diamond(center+Vector2.right*(.95f+.3f*t),.16f,new Color(1,.4f,.16f,.75f),false);
        }
        if(phase==2){float t=Mathf.Clamp01((Time.time-phaseAt)/.28f);Arc(foot,.75f+t*.65f,.3f+t*.25f,.045f,0,360,new Color(1,.65f,.24f,(1-t)*.8f),40);}
        mesh.Clear(false);mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();meshRenderer.enabled=vertices.Count>0&&solidMaterial!=null;
    }
    void Clear(){vertices.Clear();if(mesh!=null)mesh.Clear();if(meshRenderer!=null)meshRenderer.enabled=false;}
    void Vertex(Vector2 p,Color c){vertices.Add(matrix.MultiplyPoint3x4(new Vector3(p.x,p.y,-.17f)));colors.Add(c);uv.Add(Vector2.zero);}
    void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color){int i=vertices.Count;Vertex(a,color);Vertex(b,color);Vertex(c,color);Vertex(d,color);triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i);triangles.Add(i+2);triangles.Add(i+3);}
    void Segment(Vector2 a,Vector2 b,float w,Color c){var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*w*.5f;Quad(a-n,b-n,b+n,a+n,c);}
    void Arc(Vector2 center,float x,float y,float width,float from,float to,Color color,int segments)
    {for(int i=0;i<segments;i++){float a=Mathf.Lerp(from,to,(float)i/segments)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(float)(i+1)/segments)*Mathf.Deg2Rad;Segment(center+new Vector2(Mathf.Cos(a)*x,Mathf.Sin(a)*y),center+new Vector2(Mathf.Cos(b)*x,Mathf.Sin(b)*y),width,color);}}
    void Diamond(Vector2 c,float r,Color color,bool fill){var l=c+Vector2.left*r;var d=c+Vector2.down*r;var ri=c+Vector2.right*r;var u=c+Vector2.up*r;if(fill)Quad(l,d,ri,u,color);else{Segment(l,d,.025f,color);Segment(d,ri,.025f,color);Segment(ri,u,.025f,color);Segment(u,l,.025f,color);}}
    public void OnPoolGet(){EnsureResources();ownerBadge=false;CancelCharge();Clear();}
    public void OnPoolRelease(){ownerBadge=false;CancelCharge();Clear();}
    void OnDisable(){ownerBadge=false;CancelCharge();Clear();}
    void OnDestroy(){if(mesh!=null)Destroy(mesh);foreach(var echo in echoes)if(echo.mesh!=null)Destroy(echo.mesh);}
}
