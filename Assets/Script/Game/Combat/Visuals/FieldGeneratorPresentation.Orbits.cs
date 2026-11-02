using UnityEngine;

/// <summary>Counter-rotating waves and orbit arcs, with a distinct breakthrough silhouette in the same mesh.</summary>
public sealed partial class FieldGeneratorPresentation
{
    [SerializeField] bool fieldOrbitStyle;
    [SerializeField] float fieldOrbitSpeed = 24;
    [SerializeField] float breakthroughOrbitMultiplier = 1.45f;
    [SerializeField] float breakthroughBandWidth = 1.7f;
    [SerializeField] float breakthroughAreaBoost = .055f;
    float fieldOrbitAngle;
    const int WaveVertices = FieldWaveCount * 5 * (FieldSegments + 1);
    const int OrbitSegments = 12;
    const int OrbitArcCount = 4;
    const int OrbitArcVertices = 5 * (OrbitSegments + 1);
    const int CornerCount = 6;
    const int CornerVertices = 9;
    const int CornerOffset = WaveVertices + OrbitArcCount * OrbitArcVertices;
    static readonly float[] OrbitBandOffsets = { -1f,-.4f,0,.4f,1f };
    static readonly float[] OrbitBandAlpha = { 0,.3f,1,.3f,0 };
    public bool UsesFieldOrbit => surfaceFieldStyle && fieldOrbitStyle;
    public float FieldOrbitAngle => fieldOrbitAngle;
    public float FieldOrbitDegreesPerSecond => fieldOrbitSpeed * (breakthrough ? breakthroughOrbitMultiplier : 1);
    public int FieldCornerVertexOffset => CornerOffset;

    public void AuthorFieldOrbit(float speed,float breakthroughSpeedMultiplier,float breakthroughWidth,float areaBoost)
    {
        fieldOrbitStyle = true; fieldOrbitSpeed = Mathf.Clamp(speed,5,90);
        breakthroughOrbitMultiplier = Mathf.Clamp(breakthroughSpeedMultiplier,1,2);
        breakthroughBandWidth = Mathf.Clamp(breakthroughWidth,1.2f,2.2f);
        breakthroughAreaBoost = Mathf.Clamp(areaBoost,0,.1f);
    }

    void UpdateFieldOrbitBuffers(float progress,float rotation,bool upgraded,Color tint,float strength,Vector3[] vertices,Color32[] colors)
    {
        UpdateOrbitGeometry(progress,rotation,upgraded,tint,waveOpacity * (.85f+.15f*strength) + contact*.1f,breakthroughBandWidth,vertices,colors);
    }
    static Color32 FieldVertexColor(Color tint,float alpha) { tint.a = Mathf.Clamp01(alpha); return tint; }
    static void UpdateOrbitGeometry(float progress,float rotation,bool upgraded,Color tint,float opacity,float upgradedWidth,Vector3[] vertices,Color32[] colors)
    {
        int index=0; float width=upgraded?upgradedWidth:1;
        Color waveTint=Color.Lerp(tint,upgraded?new Color(1,.80f,.28f):Color.white,upgraded?.26f:.10f);
        for(int wave=0;wave<FieldWaveCount;wave++)
        {
            float t=Mathf.Repeat(progress+wave/(float)FieldWaveCount,1),scale=Mathf.Lerp(.14f,upgraded?.90f:.96f,t);
            float envelope=Mathf.Pow(Mathf.Sin(t*Mathf.PI),.65f);
            // Equal and opposite directions remain continuous when expansion wraps at the edge.
            float degrees=rotation*(wave==1?-1f:1f)+(wave==2?20:0);
            float radians=degrees*Mathf.Deg2Rad,c=Mathf.Cos(radians),s=Mathf.Sin(radians);
            for(int band=0;band<FieldBandRadii.Length;band++) for(int point=0;point<=FieldSegments;point++)
            {
                float bandRadius=.98f+(FieldBandRadii[band]-.98f)*width;
                var p=HexContour[point]; vertices[index]=new Vector3(p.x*c-p.y*s,p.x*s+p.y*c,0)*(scale*bandRadius);
                colors[index]=FieldVertexColor(waveTint,opacity*FieldBandAlpha[band]*envelope); index++;
            }
        }
        for(int arc=0;arc<OrbitArcCount;arc++)
        {
            bool inner=arc<2; float centre=inner?.43f:.89f;
            float span=upgraded?(inner?104:126):(inner?58:66);
            float heading=(arc%2)*180+(inner?rotation:-rotation)+ (inner?15:65);
            float halfWidth=(inner?.013f:.017f)*(upgraded?upgradedWidth:1);
            int arcOffset=index;
            for(int point=0;point<=OrbitSegments;point++)
            {
                float u=point/(float)OrbitSegments,angle=(heading+(u-.5f)*span)*Mathf.Deg2Rad;
                float c=Mathf.Cos(angle),s=Mathf.Sin(angle),cap=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.4f);
                for(int band=0;band<5;band++)
                {
                    int v=arcOffset+band*(OrbitSegments+1)+point; float r=centre+OrbitBandOffsets[band]*halfWidth;
                    vertices[v]=new Vector3(c*r,s*r,0);
                    Color arcTint=upgraded?Color.Lerp(new Color(1,.61f,.10f),new Color(1,.94f,.65f),band==2?.9f:.12f):Color.Lerp(tint,Color.white,.22f);
                    colors[v]=FieldVertexColor(arcTint,OrbitBandAlpha[band]*cap*(upgraded?.96f:.48f)*opacity);
                }
            }
            index+=OrbitArcVertices;
        }
        for(int corner=0;corner<CornerCount;corner++)
        {
            float degrees=corner*60-rotation, a=degrees*Mathf.Deg2Rad;
            Vector3 outward=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),side=new Vector3(-outward.y,outward.x,0),centre=outward*.93f;
            vertices[index]=centre; colors[index]=FieldVertexColor(new Color(1,.94f,.70f),upgraded?.98f:0); index++;
            for(int band=0;band<2;band++) for(int p=0;p<4;p++)
            {
                float extent=band==0?1:.62f; Vector3 offset;
                if(p==0) offset=outward*.062f; else if(p==1) offset=side*.032f; else if(p==2) offset=-outward*.075f; else offset=-side*.032f;
                vertices[index]=centre+offset*extent;
                colors[index]=FieldVertexColor(new Color(1,band==0?.46f:.78f,band==0?.04f:.20f),upgraded?(band==0?0:.88f):0); index++;
            }
        }
    }
    public static Mesh CreateFieldOrbitMesh()
    {
        var vertices=new Vector3[CornerOffset+CornerCount*CornerVertices]; var colors=new Color32[vertices.Length]; var uv=new Vector2[vertices.Length];
        int waveIndices=FieldWaveCount*4*FieldSegments*6,arcIndices=OrbitArcCount*4*OrbitSegments*6;
        var triangles=new int[waveIndices+arcIndices+CornerCount*36]; int n=0;
        for(int wave=0;wave<FieldWaveCount;wave++) for(int band=0;band<4;band++) for(int point=0;point<FieldSegments;point++)
            AddFieldQuad(triangles,ref n,wave*5*(FieldSegments+1)+band*(FieldSegments+1)+point,FieldSegments+1);
        for(int arc=0;arc<OrbitArcCount;arc++) for(int band=0;band<4;band++) for(int point=0;point<OrbitSegments;point++)
            AddFieldQuad(triangles,ref n,WaveVertices+arc*OrbitArcVertices+band*(OrbitSegments+1)+point,OrbitSegments+1);
        for(int corner=0;corner<CornerCount;corner++) for(int p=0;p<4;p++)
        {
            int b=CornerOffset+corner*CornerVertices,next=(p+1)%4;
            triangles[n++]=b;triangles[n++]=b+5+p;triangles[n++]=b+5+next;
            triangles[n++]=b+5+p;triangles[n++]=b+1+p;triangles[n++]=b+1+next;
            triangles[n++]=b+5+p;triangles[n++]=b+1+next;triangles[n++]=b+5+next;
        }
        for(int i=0;i<uv.Length;i++) uv[i]=new Vector2(.5f,.5f);
        UpdateOrbitGeometry(.15f,0,false,new Color(.08f,.90f,.17f),.88f,1.7f,vertices,colors);
        return new Mesh {name="FieldOrbitWavesV3",vertices=vertices,colors32=colors,uv=uv,triangles=triangles,bounds=new Bounds(Vector3.zero,new Vector3(2,2,.1f))};
    }
    static void AddFieldQuad(int[] triangles,ref int n,int a,int stride)
    {
        int b=a+stride;triangles[n++]=a;triangles[n++]=b;triangles[n++]=a+1;triangles[n++]=a+1;triangles[n++]=b;triangles[n++]=b+1;
    }
}
