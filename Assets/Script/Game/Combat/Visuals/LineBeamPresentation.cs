using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Reusable, texture-free presentation for the instantaneous piercing ray.
/// A single mesh combines beam layers, charge arcs, streaks and capped hit sparks.
/// No physics, damage, random-number consumption or per-cast GameObjects live here.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public sealed class LineBeamPresentation : MonoBehaviour
{
    const int MaxBeams=8,MaxImpacts=12;
    static Material sharedMaterial;
    static readonly Color[] Rainbow={
        new Color(1,.38f,.32f),new Color(1,.65f,.23f),new Color(1,.85f,.35f),
        new Color(.42f,.92f,.57f),new Color(.32f,.87f,.97f),new Color(.43f,.62f,1),new Color(.78f,.48f,1)};
    readonly Vector2[] origins=new Vector2[MaxBeams],directions=new Vector2[MaxBeams],impacts=new Vector2[MaxImpacts];
    readonly float[] lengths=new float[MaxBeams];
    readonly List<Vector3> vertices=new List<Vector3>(2048);
    readonly List<Color32> colors=new List<Color32>(2048);
    readonly List<int> triangles=new List<int>(3072);
    Mesh mesh;
    MeshRenderer meshRenderer;
    Matrix4x4 worldToLocal;
    int beamCount,impactCount;
    float castAt=-100,chargeUntil=-1,chargeAt,duration=.24f,baseWidth=.12f;
    bool variant,geometryDirty=true,resourcesUnavailable;
    float lastRenderedAt=float.NegativeInfinity;
    Vector2 castOrigin,chargeLocalOrigin,chargeDirection;
    public bool HasVisibleGeometry=>meshRenderer!=null&&meshRenderer.enabled;
    public bool HasCharge=>Time.time<chargeUntil;
    public int ActiveBeamCount=>Time.time-castAt<=duration?beamCount:0;
    public int ImpactCount=>impactCount;
    public int VertexCount=>vertices.Count;
    public Vector2 CastOrigin=>castOrigin;
    public Vector2 ChargeOrigin=>(Vector2)transform.TransformPoint(chargeLocalOrigin);
    public int MeshBuildCount{get;private set;}
    public MeshRenderer Renderer=>meshRenderer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetMaterial(){if(sharedMaterial!=null)Destroy(sharedMaterial);sharedMaterial=null;}
    void Awake(){EnsureResources();}
    bool EnsureResources()
    {
        if(resourcesUnavailable)return false;
        if(mesh==null)
        {
            mesh=new Mesh{name="line_beam_combined",hideFlags=HideFlags.DontSave};mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh=mesh;meshRenderer=GetComponent<MeshRenderer>();
            meshRenderer.enabled=false;
            meshRenderer.shadowCastingMode=ShadowCastingMode.Off;meshRenderer.receiveShadows=false;
            meshRenderer.lightProbeUsage=LightProbeUsage.Off;meshRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        if(sharedMaterial==null)
        {
            var shader=Shader.Find("Sprites/Default");
            // Both shaders support the mesh's vertex color and transparent alpha.
            if(shader==null)shader=Shader.Find("UI/Default");
            if(shader==null)
            {
                resourcesUnavailable=true;meshRenderer.enabled=false;
                Debug.LogWarning("Line beam presentation disabled: no compatible shader is available.",this);
                return false;
            }
            sharedMaterial=new Material(shader){name="line_beam_shared",hideFlags=HideFlags.HideAndDontSave};
        }
        if(meshRenderer.sharedMaterial!=sharedMaterial)meshRenderer.sharedMaterial=sharedMaterial;
        return true;
    }
    public void Configure(float width,int sortingOrder,float legacyDuration)
    {
        if(!EnsureResources())return;
        geometryDirty=true;baseWidth=Mathf.Clamp(width*1.5f,.10f,.18f);
        duration=Mathf.Clamp(legacyDuration*3,.20f,.30f);meshRenderer.sortingOrder=sortingOrder;
    }
    public void BeginCharge(Vector2 origin,Vector2 direction,float seconds)
    {
        if(!EnsureResources())return;
        geometryDirty=true;chargeLocalOrigin=transform.InverseTransformPoint(origin);
        chargeDirection=direction.sqrMagnitude>.001f?direction.normalized:Vector2.right;
        chargeAt=Time.time;chargeUntil=chargeAt+Mathf.Clamp(seconds,.02f,.12f);RenderNow();
    }
    public void BeginCast(Vector2 origin,int count,bool isVariant)
    {
        if(!EnsureResources())return;
        geometryDirty=true;beamCount=Mathf.Clamp(count,0,MaxBeams);impactCount=0;
        castOrigin=origin;variant=isVariant;castAt=Time.time;chargeUntil=-1;
        for(int i=0;i<MaxBeams;i++)lengths[i]=0;
    }
    public void SetBeam(int index,Vector2 origin,Vector2 direction,float length)
    {
        if(index<0||index>=beamCount)return;
        geometryDirty=true;origins[index]=origin;directions[index]=direction.normalized;lengths[index]=Mathf.Max(0,length);
    }
    public void AddImpact(Vector2 point)
    {
        if(impactCount>=MaxImpacts)return;
        for(int i=0;i<impactCount;i++)if((impacts[i]-point).sqrMagnitude<.10f)return;
        geometryDirty=true;impacts[impactCount++]=point;
    }
    public void CancelCharge(){geometryDirty=true;chargeUntil=-1;}
    public void Cancel()
    {
        geometryDirty=true;beamCount=impactCount=0;castAt=-100;chargeUntil=-1;
        vertices.Clear();colors.Clear();triangles.Clear();
        if(mesh!=null)mesh.Clear();if(meshRenderer!=null)meshRenderer.enabled=false;
    }
    void OnDisable(){Cancel();}
    void OnDestroy(){if(mesh!=null)Destroy(mesh);}
    void Update(){RenderNow();}
    public void RenderNow()
    {
        if(!EnsureResources())return;
        float now=Time.time;var nextWorldToLocal=transform.worldToLocalMatrix;
        // Pausing or selecting a card must not upload identical geometry every rendered frame.
        // A moving parent still triggers reprojection so fired beams remain fixed in the world.
        if(!geometryDirty&&now==lastRenderedAt&&nextWorldToLocal==worldToLocal)return;
        geometryDirty=false;lastRenderedAt=now;worldToLocal=nextWorldToLocal;
        float age=now-castAt;
        bool shot=beamCount>0&&age>=0&&age<duration,charge=Time.time<chargeUntil;
        if(!shot&&!charge){meshRenderer.enabled=false;return;}
        vertices.Clear();colors.Clear();triangles.Clear();
        if(charge)DrawCharge();
        if(shot)DrawCast(age);
        mesh.Clear(false);mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        MeshBuildCount++;
        meshRenderer.enabled=vertices.Count>0;
    }
    static Color Alpha(Color color,float alpha){color.a=Mathf.Clamp01(alpha);return color;}
    Color BeamColor(int i)=>variant?Rainbow[i%Rainbow.Length]:Color.Lerp(new Color(.25f,.78f,.94f),new Color(.48f,.91f,.82f),i*.18f);
    void DrawCharge()
    {
        Vector2 center=ChargeOrigin;
        float p=Mathf.InverseLerp(chargeAt,chargeUntil,Time.time),r=Mathf.Lerp(.48f,.17f,p);
        var warm=new Color(1,.79f,.31f,.65f);var cyan=new Color(.50f,.91f,1,.8f);
        Arc(center,r,.027f,-35+p*55,255+p*55,cyan,12);
        Arc(center,r*.72f,.022f,155-p*70,340-p*70,warm,8);
        for(int i=0;i<3;i++)
        {
            float angle=(i*120+p*45)*Mathf.Deg2Rad;Vector2 dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            Segment(center+dir*r*1.4f,center+dir*r,.025f,Alpha(cyan,.75f));
        }
        Diamond(center+chargeDirection*.14f,chargeDirection,.10f,.035f,Alpha(Color.white,p*.65f));
    }
    void DrawCast(float age)
    {
        float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration*.35f,duration,age));
        float widthScale=1-.65f*Mathf.SmoothStep(0,1,age/duration);
        float w=baseWidth*widthScale;
        // Layer all outer ribbons before all cores, so crossings stay crisp.
        for(int layer=0;layer<3;layer++)for(int i=0;i<beamCount;i++)
        {
            if(lengths[i]<.01f)continue;
            Vector2 a=origins[i],d=directions[i],b=a+d*lengths[i];Color hue=BeamColor(i);
            if(layer==0)Taper(a,b,w*1.9f,Alpha(new Color(.06f,.34f,.43f),fade*.58f));
            else if(layer==1)Taper(a,b,w*1.20f,Alpha(hue,fade*.88f));
            else Taper(a+d*.09f,b-d*.04f,w*.31f,Alpha(new Color(.96f,1,.94f),fade));
        }
        for(int i=0;i<beamCount;i++)
        {
            if(lengths[i]<.01f)continue;
            Vector2 a=origins[i],d=directions[i],n=new Vector2(-d.y,d.x),b=a+d*lengths[i];Color hue=BeamColor(i);
            float flow=Mathf.Repeat(age*13+i*.41f,1);
            for(int j=0;j<2;j++)
            {
                float along=Mathf.Repeat(flow+j*.49f,1)*lengths[i];Vector2 center=a+d*along+n*(j==0?1:-1)*w*1.35f;
                Segment(center-d*.17f,center+d*.13f,.019f,Alpha(new Color(1,.88f,.53f),fade*.74f));
            }
            if(age<.13f)
            {
                float p=1-age/.13f;Diamond(b,d,.13f+p*.15f,.07f+p*.035f,Alpha(hue,fade));
                Diamond(b,d,.08f,.027f,Alpha(Color.white,fade));
            }
        }
        // A small origin pulse signals the release without covering the character.
        float muzzle=1-Mathf.Clamp01(age/.17f);
        if(muzzle>0)
        {
            float r=.17f+age*1.6f;
            Arc(castOrigin,r,.027f,0,360,Alpha(new Color(1,.82f,.38f),muzzle*.75f),18);
            Vector2 d=beamCount>0?directions[0]:Vector2.right;
            Diamond(castOrigin+d*.13f,d,.17f*muzzle+.045f,.065f*muzzle,Alpha(new Color(.96f,1,.93f),muzzle));
        }
        float hitFade=1-Mathf.Clamp01(age/.20f);
        if(hitFade<=0)return;
        for(int i=0;i<impactCount;i++)
        {
            Vector2 point=impacts[i];float r=.09f+age*.95f;
            Arc(point,r,.018f,0,360,Alpha(new Color(1,.84f,.40f),hitFade*.6f),12);
            for(int k=0;k<4;k++)
            {
                float angle=(k*90+45+i*17)*Mathf.Deg2Rad;Vector2 d=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                Segment(point+d*r*.7f,point+d*(r+.08f*hitFade),.023f,Alpha(new Color(1,.92f,.65f),hitFade));
            }
            Diamond(point,Vector2.right,.075f*hitFade,.03f*hitFade,Alpha(Color.white,hitFade));
        }
    }
    void Taper(Vector2 a,Vector2 b,float width,Color color)
    {
        Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*width*.5f;
        Vector2 p=Vector2.Lerp(a,b,.08f),q=Vector2.Lerp(a,b,.88f);
        Quad(a+n*.45f,a-n*.45f,p-n,p+n,color);
        Quad(p+n,p-n,q-n*.65f,q+n*.65f,color);
        Quad(q+n*.65f,q-n*.65f,b-n*.06f,b+n*.06f,color);
    }
    void Segment(Vector2 a,Vector2 b,float width,Color color)
    {
        Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*width*.5f;
        Quad(a+n,a-n,b-n,b+n,color);
    }
    void Diamond(Vector2 point,Vector2 direction,float length,float width,Color color)
    {
        Vector2 n=new Vector2(-direction.y,direction.x);
        Quad(point+direction*length,point+n*width,point-direction*length,point-n*width,color);
    }
    void Arc(Vector2 center,float radius,float width,float from,float to,Color color,int steps)
    {
        for(int i=0;i<steps;i++)
        {
            float a=Mathf.Lerp(from,to,(float)i/steps)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(float)(i+1)/steps)*Mathf.Deg2Rad;
            Vector2 da=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),db=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
            Quad(center+da*(radius+width*.5f),center+da*(radius-width*.5f),center+db*(radius-width*.5f),center+db*(radius+width*.5f),color);
        }
    }
    void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
    {
        int start=vertices.Count;
        vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(a.x,a.y,-.05f)));
        vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(b.x,b.y,-.05f)));
        vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(c.x,c.y,-.05f)));
        vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(d.x,d.y,-.05f)));
        Color32 packed=color;for(int i=0;i<4;i++)colors.Add(packed);
        triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start);triangles.Add(start+2);triangles.Add(start+3);
    }
}
