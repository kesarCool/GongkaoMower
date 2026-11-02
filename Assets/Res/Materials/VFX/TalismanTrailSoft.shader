Shader "GongkaoMower/TalismanTrailSoft"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float edge = saturate(1.0 - abs(i.uv.y * 2.0 - 1.0));
                return fixed4(i.color.rgb, i.color.a * smoothstep(0.0, 0.32, edge));
            }
            ENDCG
        }
    }
}
