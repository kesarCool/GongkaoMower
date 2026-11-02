using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Scaled-time cast seal and birth portals at successful, fully constrained Spawner positions.</summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public sealed class BossSummonPresentation : MonoBehaviour,IPoolReceiver
{
    [SerializeField] Material solidMaterial;
    [SerializeField] int sortingOrder=2;
    [SerializeField] float birthSeconds=.72f,releaseSeconds=.36f;
    const int Capacity=32;
    struct Birth {public bool active;public Vector3 position;public float at;}
    readonly Birth[] births=new Birth[Capacity];
    readonly List<Vector3> vertices=new List<Vector3>(8192);
    readonly List<Color32> colors=new List<Color32>(8192);
    readonly List<Vector2> uv=new List<Vector2>(8192);
    readonly List<int> triangles=new List<int>(12288);
    Mesh mesh;MeshRenderer meshRenderer;EnemyBase enemy;EnemyWordLabel label;
    Matrix4x4 worldToLocal;float phaseAt,chargeSeconds;int phase,token,nextBirth;
    public int Phase=>phase;
    public int CastToken=>token;
    public int TotalBirthFeedback {get;private set;}
    public Vector3 LastBirthPosition {get;private set;}
    public bool HasVisibleGeometry=>meshRenderer!=null&&meshRenderer.enabled&&vertices.Count>0;
    public int ActiveBirthCount {get {int n=0;for(int i=0;i<Capacity;i++)if(births[i].active&&Time.time-births[i].at<birthSeconds)n++;return n;}}
    public bool HasBirthAt(Vector3 point,float tolerance=.001f)
    {for(int i=0;i<Capacity;i++)if(births[i].active&&Time.time-births[i].at<birthSeconds&&(births[i].position-point).sqrMagnitude<tolerance*tolerance)return true;return false;}
    void Awake(){EnsureResources();}
    void EnsureResources()
    {
        if(mesh!=null)return;
        enemy=GetComponentInParent<EnemyBase>();label=enemy!=null?enemy.GetComponentInChildren<EnemyWordLabel>(true):null;
        mesh=new Mesh{name="boss_summon_seal",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();GetComponent<MeshFilter>().sharedMesh=mesh;
        meshRenderer=GetComponent<MeshRenderer>();meshRenderer.sharedMaterial=solidMaterial;meshRenderer.sortingOrder=sortingOrder;
        meshRenderer.shadowCastingMode=ShadowCastingMode.Off;meshRenderer.receiveShadows=false;meshRenderer.lightProbeUsage=LightProbeUsage.Off;meshRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;meshRenderer.enabled=false;
    }
    public int BeginCharge(float seconds)
    {
        EnsureResources();CancelCharge();TotalBirthFeedback=0;phase=1;phaseAt=Time.time;chargeSeconds=Mathf.Max(.01f,seconds);RenderNow();return token;
    }
    public void ReleaseCharge(int castToken)
    {if(castToken!=token)return;phase=2;phaseAt=Time.time;RenderNow();}
    public void OnSummoned(GameObject spawned,int castToken)
    {
        if(castToken!=token||spawned==null||!isActiveAndEnabled||enemy==null||enemy.Hp<=0||!enemy.gameObject.activeInHierarchy)return;
        births[nextBirth]=new Birth{active=true,position=spawned.transform.position,at=Time.time};nextBirth=(nextBirth+1)%Capacity;
        LastBirthPosition=spawned.transform.position;TotalBirthFeedback++;RenderNow();
    }
    public void CancelCharge()
    {
        token++;phase=0;nextBirth=0;for(int i=0;i<Capacity;i++)births[i].active=false;Clear();
    }
    void LateUpdate(){RenderNow();}
    public void RenderNow()
    {
        EnsureResources();if(enemy==null||enemy.Hp<=0||!enemy.gameObject.activeInHierarchy){if(phase!=0||ActiveBirthCount>0)CancelCharge();else Clear();return;}
        vertices.Clear();colors.Clear();uv.Clear();triangles.Clear();worldToLocal=transform.worldToLocalMatrix;
        Vector2 center;center=label!=null&&label.TryGetVisualCenter(out center)?center:(Vector2)enemy.transform.position;
        Vector3 foot=new Vector3(center.x,center.y-.65f,enemy.transform.position.z);
        if(phase==1)
        {
            float p=Mathf.Clamp01((Time.time-phaseAt)/chargeSeconds),angle=(Time.time-phaseAt)*130;
            Arc(foot,1.34f,.44f,.10f,0,360,Tint(.26f,.025f,.015f,.95f),48);
            Arc(foot,1.34f,.44f,.045f,-90,-90+360*p,Tint(1,.25f,.05f,1),48);
            for(int i=0;i<4;i++)Arc(foot,1.08f,.33f,.045f,i*90-angle,i*90+58-angle,Tint(1,.58f,.19f,.9f),10);
            for(int i=0;i<6;i++)
            {
                float a=(i*60+angle)*Mathf.Deg2Rad;var point=foot+new Vector3(Mathf.Cos(a)*1.6f,Mathf.Sin(a)*.53f,0);
                Rune(point,.16f,Tint(.36f,.035f,.015f,.95f),.10f);Rune(point,.16f,Tint(1,.38f,.075f,.95f),.035f);
            }
            Arc(foot,Mathf.Lerp(1.9f,1.35f,p),Mathf.Lerp(.65f,.44f,p),.04f,0,360,Tint(1,.31f,.07f,.45f),48);
        }
        if(phase==2)
        {
            float p=Mathf.Clamp01((Time.time-phaseAt)/releaseSeconds);
            Arc(foot,1.34f+p*.8f,.44f+p*.28f,.065f,0,360,Tint(1,.43f,.09f,1-p),48);
            Arc(foot,1.1f+p*.5f,.33f+p*.18f,.035f,0,360,Tint(1,.8f,.35f,(1-p)*.8f),40);
            if(p>=1)phase=0;
        }
        for(int i=0;i<Capacity;i++)
        {
            if(!births[i].active)continue;float p=(Time.time-births[i].at)/birthSeconds;
            if(p>=1){births[i].active=false;continue;}DrawBirth(births[i].position,p);
        }
        if(vertices.Count==0){Clear();return;}
        mesh.Clear(false);mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();meshRenderer.enabled=solidMaterial!=null;
    }
    void DrawBirth(Vector3 position,float p)
    {
        var foot=position+Vector3.down*.48f;float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,p)),open=Mathf.Sin(Mathf.Min(1,p*3)*Mathf.PI*.5f);
        float radius=Mathf.Lerp(.32f,.8f,open),height=radius*.34f;
        Arc(foot,radius,height,.10f,0,360,Tint(.3f,.02f,.012f,fade*.95f),32);
        Arc(foot,radius,height,.045f,0,360,Tint(1,.23f,.035f,fade),32);
        for(int i=0;i<4;i++)Arc(foot,radius*.78f,height*.78f,.025f,i*90+p*140,i*90+50+p*140,Tint(1,.71f,.24f,fade),6);
        // The vertical slit is strongest at birth; the readable monster is already active above it.
        float slit=Mathf.Max(0,1-p*2.5f),width=.16f*slit;
        if(slit>0)
        {
            var low=position+Vector3.down*.52f;var high=position+Vector3.up*(.45f+.2f*p);var middle=Vector3.Lerp(low,high,.47f);
            Quad(low,middle+Vector3.left*(width+.045f),high,middle+Vector3.right*(width+.045f),Tint(.3f,.015f,.01f,slit*.85f));
            Quad(low,middle+Vector3.left*width,high,middle+Vector3.right*width,Tint(1,.2f,.025f,slit));
            Quad(low+Vector3.up*.08f,middle+Vector3.left*.025f,high-Vector3.up*.08f,middle+Vector3.right*.025f,Tint(1,.88f,.5f,slit));
        }
        for(int side=-1;side<=1;side+=2)
        {
            var a=position+new Vector3(side*(.22f+p*.55f),.12f+p*.5f,0);
            Segment(a,a+new Vector3(side*.08f,.17f,0),.045f,Tint(1,.5f,.1f,fade*.85f));
            Rune(foot+Vector3.right*side*(radius+.12f),.085f,Tint(1,.48f,.09f,fade),.025f);
        }
    }
    static Color Tint(float r,float g,float b,float a)=>new Color(r,g,b,Mathf.Clamp01(a));
    void Vertex(Vector3 p,Color c){p.z-=.16f;vertices.Add(worldToLocal.MultiplyPoint3x4(p));colors.Add(c);uv.Add(Vector2.zero);}
    void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
    {int i=vertices.Count;Vertex(a,color);Vertex(b,color);Vertex(c,color);Vertex(d,color);triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i);triangles.Add(i+2);triangles.Add(i+3);}
    void Segment(Vector3 a,Vector3 b,float width,Color color)
    {var direction=(b-a).normalized;var normal=new Vector3(-direction.y,direction.x,0)*width*.5f;Quad(a-normal,b-normal,b+normal,a+normal,color);}
    void Arc(Vector3 c,float x,float y,float width,float from,float to,Color color,int segments)
    {for(int i=0;i<segments;i++){float a=Mathf.Lerp(from,to,(float)i/segments)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(float)(i+1)/segments)*Mathf.Deg2Rad;Segment(c+new Vector3(Mathf.Cos(a)*x,Mathf.Sin(a)*y,0),c+new Vector3(Mathf.Cos(b)*x,Mathf.Sin(b)*y,0),width,color);}}
    void Rune(Vector3 c,float radius,Color color,float width)
    {var left=c+Vector3.left*radius;var down=c+Vector3.down*radius*.65f;var right=c+Vector3.right*radius;var up=c+Vector3.up*radius*.65f;Segment(left,down,width,color);Segment(down,right,width,color);Segment(right,up,width,color);Segment(up,left,width,color);}
    void Clear(){vertices.Clear();if(mesh!=null)mesh.Clear();if(meshRenderer!=null)meshRenderer.enabled=false;}
    public void OnPoolGet(){CancelCharge();TotalBirthFeedback=0;}
    public void OnPoolRelease(){CancelCharge();}
    void OnDisable(){CancelCharge();}
    void OnDestroy(){if(mesh!=null)Destroy(mesh);}
}
