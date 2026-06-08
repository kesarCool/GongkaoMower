// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Effect/Effect_MainTexMove"
{
	Properties
	{
		[Header(Set Mode)][KeywordEnum(Mesh,Particle)] _Custom2xyKey("制作模式", Float) = 1
		[Enum(AlphaBlend,10,Additive,1)]_Dst("材质模式", Float) = 10
		[Enum(UnityEngine.Rendering.CullMode)]_CullMode("剔除模式", Float) = 0
		[Enum(Default,0,On,1,Off,2)]_ZWrite("深度模式", Float) = 0
		[HDR][Header(Main Mode)]_Color("颜色", Color) = (1,1,1,1)
		_MainTex("MainTex", 2D) = "white" {}
		_MainTex_U("主贴图流动_U", Float) = 0
		_MainTex_V("主贴图流动_V", Float) = 0
		_MainTexRotator("主帖图旋转", Range( 0 , 2)) = 2
		[KeywordEnum(R,A)] _Mask_RorA("Mask1_RorA", Float) = 0
		[Header(Mask Mode)]_MaskTex("遮罩贴图", 2D) = "white" {}
		_MaskTex_U1("遮罩贴图流动_U", Float) = 0
		[KeywordEnum(On,Off)] _MaskCustomDataKey2("遮罩自定义数据开关", Float) = 1
		_MaskTex_V1("遮罩贴图流动_V", Float) = 0
		_MaskTexRotator("遮罩贴图旋转", Range( 0 , 2)) = 0
		_StencilID("StencilID", Range( 0 , 8)) = 0
		_TimeScale("TimeScale", Float) = 30
		[Enum(UnityEngine.Rendering.CompareFunction)]_StencilComp("StencilComp", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)]_StencilOp("StencilOp", Float) = 0
		[Enum(UnityEngine.Rendering.CompareFunction)]_Ztest("Ztest", Float) = 0
		[HideInInspector] _tex4coord3( "", 2D ) = "white" {}
		[HideInInspector] _tex4coord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IsEmissive" = "true"  }
		Cull [_CullMode]
		ZWrite [_ZWrite]
		Stencil
		{
			Ref [_StencilID]
			CompFront [_StencilComp]
			PassFront [_StencilOp]
			ZFailFront [_Ztest]
			CompBack [_StencilComp]
			PassBack [_StencilOp]
			ZFailBack [_Ztest]
		}
		Blend SrcAlpha [_Dst]
		
		CGPROGRAM
		#include "UnityPBSLighting.cginc"
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma shader_feature_local _CUSTOM2XYKEY_MESH _CUSTOM2XYKEY_PARTICLE
		#pragma shader_feature_local _MASK_RORA_R _MASK_RORA_A
		#pragma shader_feature_local _MASKCUSTOMDATAKEY2_ON _MASKCUSTOMDATAKEY2_OFF
		#pragma surface surf StandardCustomLighting keepalpha noshadow noambient novertexlights nolightmap  nodynlightmap nodirlightmap nofog nometa noforwardadd 
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 uv_tex4coord;
			float2 uv_texcoord;
			float4 vertexColor : COLOR;
			float4 uv3_tex4coord3;
		};

		struct SurfaceOutputCustomLightingCustom
		{
			half3 Albedo;
			half3 Normal;
			half3 Emission;
			half Metallic;
			half Smoothness;
			half Occlusion;
			half Alpha;
			Input SurfInput;
			UnityGIInput GIData;
		};

		uniform half _ZWrite;
		uniform half _CullMode;
		uniform half _Dst;
		uniform half _Ztest;
		uniform half _StencilOp;
		uniform half _StencilID;
		uniform half _StencilComp;
		uniform sampler2D _MainTex;
		uniform half _TimeScale;
		uniform half _MainTex_U;
		uniform half _MainTex_V;
		uniform float4 _MainTex_ST;
		uniform half _MainTexRotator;
		uniform half4 _Color;
		uniform sampler2D _MaskTex;
		uniform half _MaskTex_U1;
		uniform half _MaskTex_V1;
		uniform float4 _MaskTex_ST;
		uniform half _MaskTexRotator;

		inline half4 LightingStandardCustomLighting( inout SurfaceOutputCustomLightingCustom s, half3 viewDir, UnityGI gi )
		{
			UnityGIInput data = s.GIData;
			Input i = s.SurfInput;
			half4 c = 0;
			float4 uvs4_TexCoord303 = i.uv_tex4coord;
			uvs4_TexCoord303.xy = i.uv_tex4coord.xy * float2( 0,0 );
			half4 appendResult305 = (half4(uvs4_TexCoord303.z , uvs4_TexCoord303.w , 0.0 , 0.0));
			#if defined(_CUSTOM2XYKEY_MESH)
				half4 staticSwitch306 = float4( 0,0,0,0 );
			#elif defined(_CUSTOM2XYKEY_PARTICLE)
				half4 staticSwitch306 = appendResult305;
			#else
				half4 staticSwitch306 = appendResult305;
			#endif
			half4 break307 = staticSwitch306;
			half2 appendResult173 = (half2(break307.x , break307.y));
			half2 Costom1XY350 = appendResult173;
			half temp_output_479_0 = fmod( _Time.y , _TimeScale );
			half2 appendResult13 = (half2(_MainTex_U , _MainTex_V));
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			half2 MainUVMove332 = ( ( temp_output_479_0 * appendResult13 ) + uv_MainTex );
			float cos475 = cos( ( UNITY_PI * _MainTexRotator ) );
			float sin475 = sin( ( UNITY_PI * _MainTexRotator ) );
			half2 rotator475 = mul( MainUVMove332 - float2( 0.5,0.5 ) , float2x2( cos475 , -sin475 , sin475 , cos475 )) + float2( 0.5,0.5 );
			half4 tex2DNode1 = tex2D( _MainTex, ( Costom1XY350 + rotator475 ) );
			half2 appendResult383 = (half2(_MaskTex_U1 , _MaskTex_V1));
			float2 uv_MaskTex = i.uv_texcoord * _MaskTex_ST.xy + _MaskTex_ST.zw;
			float cos463 = cos( ( UNITY_PI * _MaskTexRotator ) );
			float sin463 = sin( ( UNITY_PI * _MaskTexRotator ) );
			half2 rotator463 = mul( uv_MaskTex - float2( 0.5,0.5 ) , float2x2( cos463 , -sin463 , sin463 , cos463 )) + float2( 0.5,0.5 );
			half2 appendResult446 = (half2(i.uv3_tex4coord3.x , i.uv3_tex4coord3.y));
			#if defined(_MASKCUSTOMDATAKEY2_ON)
				half2 staticSwitch447 = appendResult446;
			#elif defined(_MASKCUSTOMDATAKEY2_OFF)
				half2 staticSwitch447 = float2( 0,0 );
			#else
				half2 staticSwitch447 = float2( 0,0 );
			#endif
			half2 Costom2y393 = staticSwitch447;
			half4 tex2DNode47 = tex2D( _MaskTex, ( ( temp_output_479_0 * appendResult383 ) + rotator463 + Costom2y393 ) );
			#if defined(_MASK_RORA_R)
				half staticSwitch486 = tex2DNode47.r;
			#elif defined(_MASK_RORA_A)
				half staticSwitch486 = tex2DNode47.a;
			#else
				half staticSwitch486 = tex2DNode47.r;
			#endif
			half Mask396 = staticSwitch486;
			c.rgb = 0;
			c.a = ( tex2DNode1.a * _Color.a * i.vertexColor.a * Mask396 );
			return c;
		}

		inline void LightingStandardCustomLighting_GI( inout SurfaceOutputCustomLightingCustom s, UnityGIInput data, inout UnityGI gi )
		{
			s.GIData = data;
		}

		void surf( Input i , inout SurfaceOutputCustomLightingCustom o )
		{
			o.SurfInput = i;
			float4 uvs4_TexCoord303 = i.uv_tex4coord;
			uvs4_TexCoord303.xy = i.uv_tex4coord.xy * float2( 0,0 );
			half4 appendResult305 = (half4(uvs4_TexCoord303.z , uvs4_TexCoord303.w , 0.0 , 0.0));
			#if defined(_CUSTOM2XYKEY_MESH)
				half4 staticSwitch306 = float4( 0,0,0,0 );
			#elif defined(_CUSTOM2XYKEY_PARTICLE)
				half4 staticSwitch306 = appendResult305;
			#else
				half4 staticSwitch306 = appendResult305;
			#endif
			half4 break307 = staticSwitch306;
			half2 appendResult173 = (half2(break307.x , break307.y));
			half2 Costom1XY350 = appendResult173;
			half temp_output_479_0 = fmod( _Time.y , _TimeScale );
			half2 appendResult13 = (half2(_MainTex_U , _MainTex_V));
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			half2 MainUVMove332 = ( ( temp_output_479_0 * appendResult13 ) + uv_MainTex );
			float cos475 = cos( ( UNITY_PI * _MainTexRotator ) );
			float sin475 = sin( ( UNITY_PI * _MainTexRotator ) );
			half2 rotator475 = mul( MainUVMove332 - float2( 0.5,0.5 ) , float2x2( cos475 , -sin475 , sin475 , cos475 )) + float2( 0.5,0.5 );
			half4 tex2DNode1 = tex2D( _MainTex, ( Costom1XY350 + rotator475 ) );
			o.Emission = ( (tex2DNode1).rgb * (_Color).rgb * (i.vertexColor).rgb );
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18900
20.8;55.2;2013.6;1035.8;2617.744;1411.676;3.646916;True;True
Node;AmplifyShaderEditor.CommentaryNode;313;-2655.814,-483.3412;Inherit;False;835.0129;248.91;;4;189;393;447;446;自定义数据开关;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;308;-1539.256,-557.0051;Inherit;False;1178.722;433.0001;;6;173;307;306;305;303;350;制作模式开关;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;24;-2660.83,-76.09662;Inherit;False;1119.304;385.9007;UV流动组件;7;332;9;8;22;13;11;10;MainUV流动组件;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;11;-2609.663,74.38332;Half;False;Property;_MainTex_V;主贴图流动_V;7;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;189;-2620.726,-427.5112;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;64;-2513.558,-1385.163;Inherit;False;2047.532;620.5366;;7;396;48;50;47;473;377;486;Mask组件;1,1,1,1;0;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;303;-1520.447,-513.776;Inherit;False;0;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;10;-2610.83,2.583001;Half;False;Property;_MainTex_U;主贴图流动_U;6;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;386;-3490.145,-1018.071;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;480;-3442.247,-676.952;Inherit;False;Property;_TimeScale;TimeScale;17;0;Create;True;0;0;0;False;0;False;30;30;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;446;-2411.563,-443.1353;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;305;-1254.701,-419.603;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.CommentaryNode;377;-2495.871,-1342.305;Inherit;False;835.3764;546.0281;UV流动组件;10;392;463;387;441;383;389;382;380;469;474;UV流动组件;1,1,1,1;0;0
Node;AmplifyShaderEditor.FmodOpNode;479;-3188.253,-1067.878;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;13;-2440.004,47.7831;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;380;-2477.22,-1185.669;Half;False;Property;_MaskTex_V1;遮罩贴图流动_V;13;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;306;-1092.134,-415.8721;Inherit;False;Property;_Custom2xyKey;制作模式;0;0;Create;False;0;0;0;False;1;Header(Set Mode);False;0;1;1;True;;KeywordEnum;2;Mesh;Particle;Create;True;True;9;1;FLOAT4;0,0,0,0;False;0;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;4;FLOAT4;0,0,0,0;False;5;FLOAT4;0,0,0,0;False;6;FLOAT4;0,0,0,0;False;7;FLOAT4;0,0,0,0;False;8;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;382;-2475.489,-1269.06;Half;False;Property;_MaskTex_U1;遮罩贴图流动_U;11;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;473;-2492.896,-846.9528;Half;False;Property;_MaskTexRotator;遮罩贴图旋转;14;0;Create;False;0;0;0;False;0;False;0;0;0;2;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;22;-2263.262,29.10336;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;447;-2289.882,-443.6203;Inherit;False;Property;_MaskCustomDataKey2;遮罩自定义数据开关;12;0;Create;False;0;0;0;False;0;False;0;1;1;True;;KeywordEnum;2;On;Off;Create;True;True;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PiNode;469;-2470.018,-940.1468;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;8;-2616.964,181.538;Inherit;False;0;1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.BreakToComponentsNode;307;-865.5611,-403.5465;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.DynamicAppendNode;383;-2248.156,-1205.024;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;389;-2483.045,-1097.105;Inherit;False;0;47;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;474;-2201.632,-892.5759;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;393;-2036.354,-448.6548;Inherit;False;Costom2y;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;9;-2082.434,35.49025;Inherit;True;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;441;-2015.482,-956.1867;Inherit;False;393;Costom2y;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PiNode;476;-1483.674,411.3812;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RotatorNode;463;-2188.47,-1041.422;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;332;-1821.215,27.1769;Inherit;False;MainUVMove;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;387;-2076.623,-1296.349;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;173;-681.407,-408.6329;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;478;-1537.01,522.6088;Half;False;Property;_MainTexRotator;主帖图旋转;8;0;Create;False;0;0;0;False;0;False;2;2;0;2;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;424;-1468.561,262.7042;Inherit;False;332;MainUVMove;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;477;-1276.722,429.4448;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;392;-1932.187,-1273.364;Inherit;True;3;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;350;-538.188,-409.6169;Inherit;False;Costom1XY;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;47;-1652.81,-1165.454;Inherit;True;Property;_MaskTex;遮罩贴图;10;0;Create;False;0;0;0;False;1;Header(Mask Mode);False;-1;None;f9b2ffd8221a8584bb0198fe1857f143;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;376;-1444.437,18.78801;Inherit;False;350;Costom1XY;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RotatorNode;475;-1022.196,128.8321;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;32;-412.2119,255.1649;Inherit;False;541.0586;264.4858;;2;25;28;颜色;1,1,1,1;0;0
Node;AmplifyShaderEditor.StaticSwitch;486;-1117.505,-1024.562;Inherit;False;Property;_Mask_RorA;Mask1_RorA;9;0;Create;False;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;2;R;A;Create;True;True;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;177;-772.7057,15.60785;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;120;-453.5561,-63.33467;Inherit;False;618.6554;280;;2;1;2;主帖图;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;33;-403.4209,552.0179;Inherit;False;496.9998;273.0001;;2;29;30;顶点颜色;1,1,1,1;0;0
Node;AmplifyShaderEditor.VertexColorNode;29;-353.4219,618.0179;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;25;-278.2129,307.651;Half;False;Property;_Color;颜色;4;1;[HDR];Create;False;0;0;0;False;1;Header(Main Mode);False;1,1,1,1;0.3152813,0.4544497,0.6132076,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;1;-408.1969,-13.3347;Inherit;True;Property;_MainTex;MainTex;5;0;Create;False;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;396;-680.6852,-1036.329;Inherit;False;Mask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;34;305.9754,144.4091;Inherit;False;212;209;;1;26;自发光;1,1,1,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode;397;-230.4875,893.872;Inherit;False;396;Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;30;-133.2431,607.4048;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ComponentMaskNode;28;-94.15372,305.1652;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ComponentMaskNode;2;-57.90005,-10.06938;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;35;308.9283,455.3081;Inherit;False;212;209;;1;27;不透明度;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;27;358.9283,505.3081;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;481;1085.138,600.3052;Inherit;False;Property;_StencilComp;StencilComp;18;1;[Enum];Create;False;0;1;Option1;0;1;UnityEngine.Rendering.CompareFunction;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;483;1085.638,497.2752;Inherit;False;Property;_StencilID;StencilID;16;0;Create;False;0;0;0;True;0;False;0;0;0;8;0;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;48;-1057.676,-1192.439;Inherit;False;True;True;True;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.DesaturateOpNode;50;-1268.847,-1234.963;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;1;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;237;1084.338,391.7392;Inherit;False;Property;_Dst;材质模式;1;1;[Enum];Create;False;0;2;AlphaBlend;10;Additive;1;0;True;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;482;1086.448,695.603;Inherit;False;Property;_StencilOp;StencilOp;19;1;[Enum];Create;False;0;0;1;UnityEngine.Rendering.StencilOp;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;484;1087.803,773.5631;Inherit;False;Property;_Ztest;Ztest;20;1;[Enum];Create;False;0;0;1;UnityEngine.Rendering.CompareFunction;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;39;1085.952,302.3232;Inherit;False;Property;_CullMode;剔除模式;2;1;[Enum];Create;False;0;1;Option1;0;1;UnityEngine.Rendering.CullMode;True;0;False;0;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;264;1083.795,218.4718;Inherit;False;Property;_ZWrite;深度模式;3;1;[Enum];Create;False;0;3;Default;0;On;1;Off;2;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;26;355.9754,194.4091;Inherit;False;3;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;766.6234,216.2797;Half;False;True;-1;2;ASEMaterialInspector;0;0;CustomLighting;Effect/Effect_MainTexMove;False;False;False;False;True;True;True;True;True;True;True;True;False;False;False;False;False;False;False;False;False;Off;0;True;264;0;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.5;True;False;0;True;Transparent;;Transparent;All;14;all;True;True;True;True;0;False;-1;True;0;True;483;255;False;-1;255;False;-1;0;True;481;0;True;482;0;False;482;0;True;484;0;True;481;0;True;482;0;False;482;0;True;484;False;2;15;10;25;False;0.5;False;2;5;False;236;10;True;237;0;1;False;-1;1;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;15;-1;-1;-1;0;False;0;0;True;39;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;446;0;189;1
WireConnection;446;1;189;2
WireConnection;305;0;303;3
WireConnection;305;1;303;4
WireConnection;479;0;386;0
WireConnection;479;1;480;0
WireConnection;13;0;10;0
WireConnection;13;1;11;0
WireConnection;306;0;305;0
WireConnection;22;0;479;0
WireConnection;22;1;13;0
WireConnection;447;1;446;0
WireConnection;307;0;306;0
WireConnection;383;0;382;0
WireConnection;383;1;380;0
WireConnection;474;0;469;0
WireConnection;474;1;473;0
WireConnection;393;0;447;0
WireConnection;9;0;22;0
WireConnection;9;1;8;0
WireConnection;463;0;389;0
WireConnection;463;2;474;0
WireConnection;332;0;9;0
WireConnection;387;0;479;0
WireConnection;387;1;383;0
WireConnection;173;0;307;0
WireConnection;173;1;307;1
WireConnection;477;0;476;0
WireConnection;477;1;478;0
WireConnection;392;0;387;0
WireConnection;392;1;463;0
WireConnection;392;2;441;0
WireConnection;350;0;173;0
WireConnection;47;1;392;0
WireConnection;475;0;424;0
WireConnection;475;2;477;0
WireConnection;486;1;47;1
WireConnection;486;0;47;4
WireConnection;177;0;376;0
WireConnection;177;1;475;0
WireConnection;1;1;177;0
WireConnection;396;0;486;0
WireConnection;30;0;29;0
WireConnection;28;0;25;0
WireConnection;2;0;1;0
WireConnection;27;0;1;4
WireConnection;27;1;25;4
WireConnection;27;2;29;4
WireConnection;27;3;397;0
WireConnection;48;0;47;0
WireConnection;50;0;47;0
WireConnection;26;0;2;0
WireConnection;26;1;28;0
WireConnection;26;2;30;0
WireConnection;0;2;26;0
WireConnection;0;9;27;0
ASEEND*/
//CHKSM=27C986D466259F7745E3D5B8172F366BDECE73CD