Shader "Custom/AdvancedHDR_Mask"
{
    Properties
    {
        _MainTex("Main Tex",2D) = "white" {}
        [HDR]_MaskColor ("Mask Color", Color) = (0,0,0,0.667)
        [HDR]_HoleColor ("Hole Color", Color) = (0.5,0.5,0,1)
        _HoleRadius("Hole Radius", Range(0,2)) = 0.03
        _SoftEdge("Edge Softness", Range(0,0.5)) = 0.032
        _Center("Center Point", Vector) = (0.5,0.5,0,0)
        _HoleColorAlpha("Hole Color Alpha", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off

        // 模板测试防止颜色混合残留
        Stencil {
            Ref 1
            Comp Always
            Pass Replace
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ UNITY_COLORSPACE_GAMMA
            #include "UnityCG.cginc"
            #include "UnityStandardUtils.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            half4 _MaskColor, _HoleColor;
            float _HoleRadius;
            float _SoftEdge;
            float2 _Center;
            float _HoleColorAlpha;

            float smootherstep(float edge0, float edge1, float x) 
            {
                x = clamp((x - edge0)/(edge1 - edge0), 0.0, 1.0);
                return x*x*x*(x*(x*6.0 - 15.0) + 10.0);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float dist = length(i.uv - _Center) * (1.0 + 1e-5 * frac(_Time.y));
                
                half4 color = tex2D(_MainTex,i.uv);

                #if !UNITY_COLORSPACE_GAMMA
                color.rgb = GammaToLinearSpace(color.rgb);
                #endif

                float edge = smoothstep(_HoleRadius - _SoftEdge, _HoleRadius + _SoftEdge, dist);
                color.rgb = lerp(_HoleColor.rgb * _HoleColorAlpha, _MaskColor.rgb, edge);
                color.a = edge * lerp(_HoleColor.a, _MaskColor.a, edge);

                color.rgb = min(color.rgb, 10.0); // 限制最大亮度值
                
                return color;
            }
            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}