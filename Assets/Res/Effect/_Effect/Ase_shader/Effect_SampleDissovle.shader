// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Effect/Effect_SampleDissovle"
{
	Properties
	{
		[Header(Set Mode)][KeywordEnum(Mesh,Particle)] _Custom2xyKey("制作模式", Float) = 1
		[Enum(AlphaBlend,10,Additive,1)]_Dst("材质模式", Float) = 10
		[Enum(UnityEngine.Rendering.CullMode)]_CullMode("剔除模式", Float) = 2
		[Enum(Default,0,On,1,Off,2)]_ZWrite("深度模式", Float) = 0
		[KeywordEnum(Off,On)] _if_ModeDepthFade1("边缘虚化开关", Float) = 0
		[HDR][Header(Main Mode)]_Color("颜色", Color) = (1,1,1,1)
		_MainTex("主贴图", 2D) = "white" {}
		_MainTexRotator("主帖图旋转", Range( 0 , 2)) = 0
		_MainTex_U("主贴图流动_U", Float) = 0
		_MainTex_V("主贴图流动_V", Float) = 0
		[Toggle(_MAINTEXTODISSOLVE1_ON)] _MainTexToDissolve1("主贴图流动影响溶解", Float) = 0
		[Header(Dissslve Mode)]_DissolveTex("溶解贴图", 2D) = "white" {}
		_DissolveTex_U1("溶解贴图流动_U", Float) = 0
		_DissolveTex_V1("溶解贴图流动_V", Float) = 0
		_DissolveIntensityCustom1z("溶解强度", Range( 0 , 1)) = 0
		[KeywordEnum(R,A)] _Mask_RorA("Mask1_RorA", Float) = 0
		_SoftaDissolve("软硬边强度", Range( 0 , 1)) = 0
		_Distortion_Tex("Distortion_Tex", 2D) = "white" {}
		[KeywordEnum(UV,Distortion)] _UvorDistortion("_UvorDistortion", Float) = 0
		_Distortion_U("Distortion_U", Float) = 0
		_Distortion_V("Distortion_V", Float) = 0
		_DistortionMask("DistortionMask", 2D) = "white" {}
		DistortionMaskTiling("DistortionMaskTiling", Vector) = (0,0,0,0)
		_Distortion_Intensity("Distortion_Intensity", Float) = 0
		_DistortionMask_Offset_V1("_DistortionMask_Offset_V", Float) = 0
		_DistortionMask_Offset_U("_DistortionMask_Offset_U", Float) = 0
		[KeywordEnum(On,Off)] _MaskCustomDataKey2("遮罩自定义数据开关", Float) = 1
		[Header(Mask Mode)]_MaskTex("遮罩贴图", 2D) = "white" {}
		_MaskTex_U1("遮罩贴图流动_U", Float) = 0
		_MaskTex_V1("遮罩贴图流动_V", Float) = 0
		_MaskTex1Rotator1("遮罩贴图1旋转", Range( 0 , 2)) = 0
		[Toggle(_MASKPOLARSWICH_ON)] _MaskPolarSwich("MaskPolarSwich", Float) = 0
		_PolarOffset1("PolarOffset", Float) = 0
		_PolarScale("PolarScale", Float) = 0
		_TimeFmod("TimeFmod", Float) = 30
		_StencilID("StencilID", Range( 0 , 8)) = 0
		[Enum(UnityEngine.Rendering.CompareFunction)]_StencilComp("StencilComp", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)]_StencilOp("StencilOp", Float) = 0
		[Enum(UnityEngine.Rendering.CompareFunction)]_Ztest("Ztest", Float) = 0
		[HideInInspector] _tex4coord2( "", 2D ) = "white" {}
		[HideInInspector] _tex4coord3( "", 2D ) = "white" {}
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
		#pragma shader_feature_local _UVORDISTORTION_UV _UVORDISTORTION_DISTORTION
		#pragma shader_feature_local _CUSTOM2XYKEY_MESH _CUSTOM2XYKEY_PARTICLE
		#pragma shader_feature_local _IF_MODEDEPTHFADE1_OFF _IF_MODEDEPTHFADE1_ON
		#pragma shader_feature_local _MASK_RORA_R _MASK_RORA_A
		#pragma shader_feature_local _MASKPOLARSWICH_ON
		#pragma shader_feature_local _MAINTEXTODISSOLVE1_ON
		#pragma shader_feature_local _MASKCUSTOMDATAKEY2_ON _MASKCUSTOMDATAKEY2_OFF
		#pragma surface surf StandardCustomLighting keepalpha noshadow noambient novertexlights nolightmap  nodynlightmap nodirlightmap nofog nometa noforwardadd 
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float2 uv_texcoord;
			float4 uv2_tex4coord2;
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

		uniform half _Dst;
		uniform half _StencilOp;
		uniform half _StencilComp;
		uniform half _CullMode;
		uniform half _ZWrite;
		uniform half _Ztest;
		uniform half _StencilID;
		uniform sampler2D _MainTex;
		uniform half _TimeFmod;
		uniform half _MainTex_U;
		uniform half _MainTex_V;
		uniform float4 _MainTex_ST;
		uniform half _MainTexRotator;
		uniform sampler2D _Distortion_Tex;
		uniform half _Distortion_U;
		uniform half _Distortion_V;
		uniform float4 _Distortion_Tex_ST;
		uniform half _Distortion_Intensity;
		uniform sampler2D _DistortionMask;
		uniform half2 DistortionMaskTiling;
		uniform half _DistortionMask_Offset_U;
		uniform half _DistortionMask_Offset_V1;
		uniform half4 _Color;
		uniform sampler2D _MaskTex;
		uniform half _MaskTex_U1;
		uniform half _MaskTex_V1;
		uniform float4 _MaskTex_ST;
		uniform half _MaskTex1Rotator1;
		uniform half _PolarScale;
		uniform half _PolarOffset1;
		uniform half _SoftaDissolve;
		uniform sampler2D _DissolveTex;
		uniform half _DissolveTex_U1;
		uniform half _DissolveTex_V1;
		uniform float4 _DissolveTex_ST;
		uniform half _DissolveIntensityCustom1z;

		inline half4 LightingStandardCustomLighting( inout SurfaceOutputCustomLightingCustom s, half3 viewDir, UnityGI gi )
		{
			UnityGIInput data = s.GIData;
			Input i = s.SurfInput;
			half4 c = 0;
			half temp_output_480_0 = fmod( _Time.y , _TimeFmod );
			half2 appendResult13 = (half2(_MainTex_U , _MainTex_V));
			half2 temp_output_22_0 = ( temp_output_480_0 * appendResult13 );
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float cos482 = cos( ( UNITY_PI * _MainTexRotator ) );
			float sin482 = sin( ( UNITY_PI * _MainTexRotator ) );
			half2 rotator482 = mul( uv_MainTex - float2( 0.5,0.5 ) , float2x2( cos482 , -sin482 , sin482 , cos482 )) + float2( 0.5,0.5 );
			half2 appendResult493 = (half2(_Distortion_U , _Distortion_V));
			float2 uv_Distortion_Tex = i.uv_texcoord * _Distortion_Tex_ST.xy + _Distortion_Tex_ST.zw;
			half2 panner495 = ( temp_output_480_0 * appendResult493 + uv_Distortion_Tex);
			half2 temp_cast_11 = (( DistortionMaskTiling.x + DistortionMaskTiling.y )).xx;
			half2 appendResult544 = (half2(_DistortionMask_Offset_U , _DistortionMask_Offset_V1));
			float2 uv_TexCoord539 = i.uv_texcoord * temp_cast_11 + appendResult544;
			#if defined(_UVORDISTORTION_UV)
				half4 staticSwitch500 = half4( temp_output_22_0, 0.0 , 0.0 );
			#elif defined(_UVORDISTORTION_DISTORTION)
				half4 staticSwitch500 = ( tex2D( _Distortion_Tex, panner495 ).r * ( _Distortion_Intensity * tex2D( _DistortionMask, uv_TexCoord539 ) ) );
			#else
				half4 staticSwitch500 = half4( temp_output_22_0, 0.0 , 0.0 );
			#endif
			half4 appendResult305 = (half4(i.uv2_tex4coord2.x , i.uv2_tex4coord2.y , i.uv2_tex4coord2.z , i.uv2_tex4coord2.w));
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
			half4 MainUVMove332 = ( half4( temp_output_22_0, 0.0 , 0.0 ) + half4( rotator482, 0.0 , 0.0 ) + staticSwitch500 + half4( Costom1XY350, 0.0 , 0.0 ) );
			half4 tex2DNode1 = tex2D( _MainTex, MainUVMove332.rg );
			half2 appendResult383 = (half2(_MaskTex_U1 , _MaskTex_V1));
			float2 uv_MaskTex = i.uv_texcoord * _MaskTex_ST.xy + _MaskTex_ST.zw;
			float cos464 = cos( ( UNITY_PI * _MaskTex1Rotator1 ) );
			float sin464 = sin( ( UNITY_PI * _MaskTex1Rotator1 ) );
			half2 rotator464 = mul( uv_MaskTex - float2( 0.5,0.5 ) , float2x2( cos464 , -sin464 , sin464 , cos464 )) + float2( 0.5,0.5 );
			half2 temp_output_454_0 = (rotator464*2.0 + -1.0);
			half2 break455 = temp_output_454_0;
			half2 appendResult463 = (half2(length( temp_output_454_0 ) , ( ( atan2( break455.y , break455.x ) / ( 2.0 * UNITY_PI ) ) + 0.0 )));
			#ifdef _MASKPOLARSWICH_ON
				half2 staticSwitch466 = (appendResult463*_PolarScale + _PolarOffset1);
			#else
				half2 staticSwitch466 = uv_MaskTex;
			#endif
			half2 appendResult503 = (half2(i.uv3_tex4coord3.z , i.uv3_tex4coord3.w));
			half2 Costom2zw501 = appendResult503;
			half4 tex2DNode47 = tex2D( _MaskTex, ( ( temp_output_480_0 * appendResult383 ) + staticSwitch466 + Costom2zw501 ) );
			#if defined(_MASK_RORA_R)
				half staticSwitch512 = tex2DNode47.r;
			#elif defined(_MASK_RORA_A)
				half staticSwitch512 = tex2DNode47.a;
			#else
				half staticSwitch512 = tex2DNode47.r;
			#endif
			half Mask396 = staticSwitch512;
			half temp_output_27_0 = ( tex2DNode1.a * _Color.a * i.vertexColor.a * Mask396 );
			#if defined(_IF_MODEDEPTHFADE1_OFF)
				half staticSwitch275 = temp_output_27_0;
			#elif defined(_IF_MODEDEPTHFADE1_ON)
				half staticSwitch275 = temp_output_27_0;
			#else
				half staticSwitch275 = temp_output_27_0;
			#endif
			#ifdef _MAINTEXTODISSOLVE1_ON
				half4 staticSwitch412 = MainUVMove332;
			#else
				half4 staticSwitch412 = float4( 0,0,0,0 );
			#endif
			half2 appendResult371 = (half2(_DissolveTex_U1 , _DissolveTex_V1));
			float2 uv_DissolveTex = i.uv_texcoord * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
			half2 appendResult446 = (half2(i.uv3_tex4coord3.x , i.uv3_tex4coord3.y));
			#if defined(_MASKCUSTOMDATAKEY2_ON)
				half2 staticSwitch447 = appendResult446;
			#elif defined(_MASKCUSTOMDATAKEY2_OFF)
				half2 staticSwitch447 = float2( 0,0 );
			#else
				half2 staticSwitch447 = float2( 0,0 );
			#endif
			half2 Costom2xy393 = staticSwitch447;
			half3 desaturateInitialColor125 = tex2D( _DissolveTex, ( staticSwitch412 + half4( ( ( temp_output_480_0 * appendResult371 ) + uv_DissolveTex + Costom2xy393 ), 0.0 , 0.0 ) ).rg ).rgb;
			half desaturateDot125 = dot( desaturateInitialColor125, float3( 0.299, 0.587, 0.114 ));
			half3 desaturateVar125 = lerp( desaturateInitialColor125, desaturateDot125.xxx, 1.0 );
			half Costom1W450 = break307.w;
			half clampResult130 = clamp( ( ( (desaturateVar125).x + 1.0 ) - ( 2.0 * ( _DissolveIntensityCustom1z + Costom1W450 ) ) ) , 0.0 , 1.0 );
			half smoothstepResult142 = smoothstep( 0.0 , ( 1.0 - _SoftaDissolve ) , clampResult130);
			half Dissolve345 = smoothstepResult142;
			c.rgb = 0;
			c.a = saturate( ( staticSwitch275 * Dissolve345 ) );
			return c;
		}

		inline void LightingStandardCustomLighting_GI( inout SurfaceOutputCustomLightingCustom s, UnityGIInput data, inout UnityGI gi )
		{
			s.GIData = data;
		}

		void surf( Input i , inout SurfaceOutputCustomLightingCustom o )
		{
			o.SurfInput = i;
			half temp_output_480_0 = fmod( _Time.y , _TimeFmod );
			half2 appendResult13 = (half2(_MainTex_U , _MainTex_V));
			half2 temp_output_22_0 = ( temp_output_480_0 * appendResult13 );
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float cos482 = cos( ( UNITY_PI * _MainTexRotator ) );
			float sin482 = sin( ( UNITY_PI * _MainTexRotator ) );
			half2 rotator482 = mul( uv_MainTex - float2( 0.5,0.5 ) , float2x2( cos482 , -sin482 , sin482 , cos482 )) + float2( 0.5,0.5 );
			half2 appendResult493 = (half2(_Distortion_U , _Distortion_V));
			float2 uv_Distortion_Tex = i.uv_texcoord * _Distortion_Tex_ST.xy + _Distortion_Tex_ST.zw;
			half2 panner495 = ( temp_output_480_0 * appendResult493 + uv_Distortion_Tex);
			half2 temp_cast_4 = (( DistortionMaskTiling.x + DistortionMaskTiling.y )).xx;
			half2 appendResult544 = (half2(_DistortionMask_Offset_U , _DistortionMask_Offset_V1));
			float2 uv_TexCoord539 = i.uv_texcoord * temp_cast_4 + appendResult544;
			#if defined(_UVORDISTORTION_UV)
				half4 staticSwitch500 = half4( temp_output_22_0, 0.0 , 0.0 );
			#elif defined(_UVORDISTORTION_DISTORTION)
				half4 staticSwitch500 = ( tex2D( _Distortion_Tex, panner495 ).r * ( _Distortion_Intensity * tex2D( _DistortionMask, uv_TexCoord539 ) ) );
			#else
				half4 staticSwitch500 = half4( temp_output_22_0, 0.0 , 0.0 );
			#endif
			half4 appendResult305 = (half4(i.uv2_tex4coord2.x , i.uv2_tex4coord2.y , i.uv2_tex4coord2.z , i.uv2_tex4coord2.w));
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
			half4 MainUVMove332 = ( half4( temp_output_22_0, 0.0 , 0.0 ) + half4( rotator482, 0.0 , 0.0 ) + staticSwitch500 + half4( Costom1XY350, 0.0 , 0.0 ) );
			half4 tex2DNode1 = tex2D( _MainTex, MainUVMove332.rg );
			o.Emission = ( (tex2DNode1).rgb * (_Color).rgb * (i.vertexColor).rgb );
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18900
2273.6;118.4;2048;1091;3388.694;352.2191;1.3;True;True
Node;AmplifyShaderEditor.CommentaryNode;308;-1748.869,-623.5464;Inherit;False;1178.722;433.0001;;8;173;307;306;305;303;304;350;450;制作模式开关;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;542;-3330.886,627.8843;Inherit;False;Property;_DistortionMask_Offset_U;_DistortionMask_Offset_U;25;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;545;-3631.617,411.4578;Inherit;False;Property;DistortionMaskTiling;DistortionMaskTiling;22;0;Create;False;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;304;-1705.044,-558.5325;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;543;-3329.886,728.8843;Inherit;False;Property;_DistortionMask_Offset_V1;_DistortionMask_Offset_V;24;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;544;-3093.886,651.8843;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;546;-3345.617,406.4578;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;491;-3395.973,291.2585;Inherit;False;Property;_Distortion_V;Distortion_V;20;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;370;-3427.796,-1605.969;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;481;-3570.395,-1275.392;Inherit;True;Property;_TimeFmod;TimeFmod;34;0;Create;True;0;0;0;False;0;False;30;30;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;492;-3390.952,185.4669;Inherit;False;Property;_Distortion_U;Distortion_U;19;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;305;-1437.922,-531.5924;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;539;-3176.609,407.1701;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;493;-3190.297,212.0712;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;24;-500.545,-619.1545;Inherit;False;1119.304;385.9007;UV流动组件;9;332;9;8;22;13;11;10;482;375;MainUV流动组件;1,1,1,1;0;0
Node;AmplifyShaderEditor.StaticSwitch;306;-1305.562,-545.1556;Inherit;False;Property;_Custom2xyKey;制作模式;0;0;Create;False;0;0;0;False;1;Header(Set Mode);False;0;1;0;True;;KeywordEnum;2;Mesh;Particle;Create;True;True;9;1;FLOAT4;0,0,0,0;False;0;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;4;FLOAT4;0,0,0,0;False;5;FLOAT4;0,0,0,0;False;6;FLOAT4;0,0,0,0;False;7;FLOAT4;0,0,0,0;False;8;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.FmodOpNode;480;-2931.925,-959.0922;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;498;-3391.547,-26.00147;Inherit;False;0;496;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;11;-488.6272,-524.9239;Half;False;Property;_MainTex_V;主贴图流动_V;9;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;495;-2963.036,-9.793022;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;10;-489.2564,-604.6723;Half;False;Property;_MainTex_U;主贴图流动_U;8;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;497;-2712.64,203.8203;Inherit;False;Property;_Distortion_Intensity;Distortion_Intensity;23;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PiNode;451;-1904,-1904;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;541;-2888.658,371.7658;Inherit;True;Property;_DistortionMask;DistortionMask;21;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.BreakToComponentsNode;307;-1076.896,-557.8229;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.CommentaryNode;64;-1741.927,-2424.01;Inherit;False;2092.733;879.9821;;17;396;47;377;466;462;465;461;463;459;460;457;454;464;453;458;512;455;Mask组件;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;452;-1951.698,-1808.739;Half;False;Property;_MaskTex1Rotator1;遮罩贴图1旋转;30;0;Create;False;0;0;0;False;0;False;0;0;0;2;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;540;-2479.658,259.7658;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;453;-1680,-1872;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;389;-1917.04,-2129.788;Inherit;False;0;47;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PiNode;484;-762.1627,-250.3643;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;313;-2742.399,-562.5493;Inherit;False;812.0739;258.0855;;6;393;189;446;447;501;503;自定义数据开关;1,1,1,1;0;0
Node;AmplifyShaderEditor.SamplerNode;496;-2731.072,-31.28489;Inherit;True;Property;_Distortion_Tex;Distortion_Tex;17;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;13;-287.8292,-576.9794;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;173;-928.6129,-559.7057;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;485;-851.5244,-175.4003;Half;False;Property;_MainTexRotator;主帖图旋转;7;0;Create;False;0;0;0;False;0;False;0;0;0;2;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;350;-784.7843,-572.4865;Inherit;False;Costom1XY;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;490;-2295.336,110.2922;Inherit;True;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;189;-2723.045,-517.7557;Inherit;False;2;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;22;-77.82153,-571.8022;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;483;-512.2498,-249.432;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;161;-1814.799,-1311.294;Inherit;False;2671.95;608.1709;;19;142;130;147;190;144;310;127;311;124;132;123;126;125;412;344;405;367;448;449;溶解;1,1,1,1;0;0
Node;AmplifyShaderEditor.RotatorNode;464;-1580.857,-2016.381;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;8;-517.139,-450.9173;Inherit;False;0;1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;367;-1806.719,-1074.487;Inherit;False;773.2299;347.313;UV流动组件;7;373;372;374;371;369;368;511;UV流动组件;1,1,1,1;0;0
Node;AmplifyShaderEditor.StaticSwitch;500;-1949.441,85.3278;Inherit;False;Property;_UvorDistortion;_UvorDistortion;18;0;Create;False;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;2;UV;Distortion;Create;True;False;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RotatorNode;482;-242.5645,-482.5352;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0.5,0.5;False;2;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;454;-1520.343,-1781.612;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT;2;False;2;FLOAT;-1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;375;-236.2972,-339.4007;Inherit;False;350;Costom1XY;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;446;-2513.035,-517.299;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;9;138.7593,-538.8369;Inherit;True;4;4;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;COLOR;0,0,0,0;False;3;FLOAT2;0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.BreakToComponentsNode;455;-1287.132,-1701.666;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.StaticSwitch;447;-2368.993,-524.5831;Inherit;False;Property;_MaskCustomDataKey2;遮罩自定义数据开关;26;0;Create;False;0;0;0;False;0;False;0;1;1;True;;KeywordEnum;2;On;Off;Create;True;True;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;369;-1756.721,-995.808;Half;False;Property;_DissolveTex_U1;溶解贴图流动_U;12;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;368;-1755.552,-924.007;Half;False;Property;_DissolveTex_V1;溶解贴图流动_V;13;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;332;424.0714,-542.4794;Inherit;False;MainUVMove;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.PiNode;456;-1086.879,-1550.985;Inherit;False;1;0;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.ATan2OpNode;457;-1099.653,-1775.904;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;371;-1585.895,-950.6078;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;393;-2107.797,-524.7211;Inherit;False;Costom2xy;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;405;-1807.127,-1275.776;Inherit;False;332;MainUVMove;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;372;-1441.161,-1018.756;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;374;-1602.248,-849.2622;Inherit;False;0;123;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleDivideOpNode;458;-806.774,-1773.638;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;511;-1370.823,-820.828;Inherit;False;393;Costom2xy;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;459;-549.2073,-1695.367;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;377;-1724.24,-2381.153;Inherit;False;813.3964;389.2258;UV流动组件;6;392;387;383;382;380;441;UV流动组件;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleAddOpNode;373;-1277.145,-967.921;Inherit;False;3;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;412;-1388.534,-1267.85;Inherit;False;Property;_MainTexToDissolve1;主贴图流动影响溶解;10;0;Create;False;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LengthOpNode;460;-1302.336,-1906.583;Inherit;True;1;0;FLOAT2;0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;462;-743.249,-1896.504;Half;False;Property;_PolarScale;PolarScale;33;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;463;-581.2228,-1805.997;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;503;-2511.395,-420.5204;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;344;-945.0222,-1189.919;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT2;0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;382;-1647.348,-2289.07;Half;False;Property;_MaskTex_U1;遮罩贴图流动_U;28;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;461;-377.6053,-1698.172;Half;False;Property;_PolarOffset1;PolarOffset;32;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;380;-1646.181,-2217.27;Half;False;Property;_MaskTex_V1;遮罩贴图流动_V;29;0;Create;False;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;465;-385.8182,-1936.917;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;501;-2113.193,-422.8525;Inherit;False;Costom2zw;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;383;-1476.525,-2243.871;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;123;-839.7258,-1240.629;Inherit;True;Property;_DissolveTex;溶解贴图;11;0;Create;False;0;0;0;False;1;Header(Dissslve Mode);False;-1;3a53b561f35674e4aa4cd567bc03b855;3a53b561f35674e4aa4cd567bc03b855;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;450;-788.7779,-454.717;Inherit;False;Costom1W;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;441;-1450.583,-2154.951;Inherit;False;501;Costom2zw;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DesaturateOpNode;125;-511.6513,-1236.81;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;1;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StaticSwitch;466;-1315.645,-2064.133;Inherit;False;Property;_MaskPolarSwich;MaskPolarSwich;31;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;387;-1337.411,-2331.09;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;132;-522.4005,-890.6515;Half;False;Property;_DissolveIntensityCustom1z;溶解强度;14;0;Create;False;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;449;-460.4003,-809.1691;Inherit;False;450;Costom1W;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;311;-194.2881,-962.8177;Inherit;False;Constant;_Float1;Float 1;54;0;Create;True;0;0;0;False;0;False;2;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;127;-325.3355,-1061.8;Half;False;Constant;_Float0;Float 0;26;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;392;-1087.633,-2333.636;Inherit;True;3;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;448;-191.6753,-868.7234;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;124;-349.7543,-1286.896;Inherit;True;True;False;False;False;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;47;-839.2993,-2333.41;Inherit;True;Property;_MaskTex;遮罩贴图;27;0;Create;False;0;0;0;False;1;Header(Mask Mode);False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;310;-22.30667,-961.2867;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;126;-71.09644,-1280.21;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;512;-403.3895,-2278.71;Inherit;False;Property;_Mask_RorA;Mask1_RorA;15;0;Create;False;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;2;R;A;Create;True;True;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;144;25.3725,-819.3442;Half;False;Property;_SoftaDissolve;软硬边强度;16;0;Create;False;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;190;93.77251,-1291.383;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;424;-2049.102,-170.868;Inherit;False;332;MainUVMove;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;396;30.9548,-2084.231;Inherit;False;Mask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;147;321.0586,-1049.078;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;120;-1540.361,-63.45503;Inherit;False;618.6554;280;;2;1;2;主帖图;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;32;-1530.187,229.0695;Inherit;False;541.0586;264.4858;;2;25;28;颜色;1,1,1,1;0;0
Node;AmplifyShaderEditor.ClampOpNode;130;258.55,-1291.422;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;33;-1526.591,523.3249;Inherit;False;496.9998;273.0001;;2;29;30;顶点颜色;1,1,1,1;0;0
Node;AmplifyShaderEditor.SamplerNode;1;-1479.7,-20.01289;Inherit;True;Property;_MainTex;主贴图;6;0;Create;False;0;0;0;False;0;False;-1;None;b063c99c1a31c2344b24a1fd767280ac;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;35;-668.0057,433.1125;Inherit;False;212;209;;1;27;不透明度;1,1,1,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode;397;-1356.255,862.5814;Inherit;False;396;Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;25;-1504.506,285.6174;Half;False;Property;_Color;颜色;5;1;[HDR];Create;False;0;0;0;False;1;Header(Main Mode);False;1,1,1,1;1,1,1,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.VertexColorNode;29;-1476.592,589.325;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SmoothstepOpNode;142;597.5026,-1230.053;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;27;-618.0057,483.1125;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;345;869.9177,-1230.98;Inherit;False;Dissolve;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;275;-26.45651,290.7808;Inherit;False;Property;_if_ModeDepthFade1;边缘虚化开关;4;0;Create;False;0;0;0;False;0;False;0;0;0;True;;KeywordEnum;2;Off;On;Create;True;True;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;346;-196.2462,654.8755;Inherit;False;345;Dissolve;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;30;-1257.979,578.7117;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ComponentMaskNode;2;-1144.705,-10.18974;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ComponentMaskNode;28;-1212.129,279.0697;Inherit;False;True;True;True;False;1;0;COLOR;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;326;396.1086,119.6506;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;34;-283.8694,-69.95557;Inherit;False;212;247;;1;26;自发光;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;237;1502.818,196.9169;Inherit;False;Property;_Dst;材质模式;1;1;[Enum];Create;False;0;2;AlphaBlend;10;Additive;1;0;True;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;476;1513.484,492.5678;Inherit;False;Property;_StencilOp;StencilOp;38;1;[Enum];Create;False;0;0;1;UnityEngine.Rendering.StencilOp;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;475;1512.174,394.2994;Inherit;False;Property;_StencilComp;StencilComp;37;1;[Enum];Create;False;0;1;Option1;0;1;UnityEngine.Rendering.CompareFunction;True;0;False;0;8;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;39;1504.432,107.1907;Inherit;False;Property;_CullMode;剔除模式;2;1;[Enum];Create;False;0;1;Option1;0;1;UnityEngine.Rendering.CullMode;True;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;26;-271.5695,-32.75558;Inherit;True;3;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SaturateNode;315;674.5404,122.0569;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;264;1498.351,23.6346;Inherit;False;Property;_ZWrite;深度模式;3;1;[Enum];Create;False;0;3;Default;0;On;1;Off;2;0;True;0;False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;478;1513.486,567.2519;Inherit;False;Property;_Ztest;Ztest;39;1;[Enum];Create;False;0;0;1;UnityEngine.Rendering.CompareFunction;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;474;1511.805,292.8828;Inherit;False;Property;_StencilID;StencilID;36;0;Create;False;0;0;0;True;0;False;0;0;0;8;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;303;-1711.741,-369.2152;Inherit;False;0;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;518;-2028.963,-88.66328;Inherit;False;345;Dissolve;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;1039.507,-68.39246;Half;False;True;-1;2;ASEMaterialInspector;0;0;CustomLighting;Effect/Effect_SampleDissovle;False;False;False;False;True;True;True;True;True;True;True;True;False;False;False;False;False;False;False;False;False;Off;0;True;264;0;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.5;True;False;0;True;Transparent;;Transparent;All;14;all;True;True;True;True;0;False;477;True;255;True;474;255;False;470;255;False;470;0;True;475;0;True;476;0;False;-1;0;True;478;0;True;475;0;True;476;0;False;-1;0;True;478;False;2;15;10;25;False;0.5;False;2;5;False;236;10;True;237;0;1;False;-1;1;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;35;-1;-1;-1;0;False;0;0;True;39;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;544;0;542;0
WireConnection;544;1;543;0
WireConnection;546;0;545;1
WireConnection;546;1;545;2
WireConnection;305;0;304;1
WireConnection;305;1;304;2
WireConnection;305;2;304;3
WireConnection;305;3;304;4
WireConnection;539;0;546;0
WireConnection;539;1;544;0
WireConnection;493;0;492;0
WireConnection;493;1;491;0
WireConnection;306;0;305;0
WireConnection;480;0;370;0
WireConnection;480;1;481;0
WireConnection;495;0;498;0
WireConnection;495;2;493;0
WireConnection;495;1;480;0
WireConnection;541;1;539;0
WireConnection;307;0;306;0
WireConnection;540;0;497;0
WireConnection;540;1;541;0
WireConnection;453;0;451;0
WireConnection;453;1;452;0
WireConnection;496;1;495;0
WireConnection;13;0;10;0
WireConnection;13;1;11;0
WireConnection;173;0;307;0
WireConnection;173;1;307;1
WireConnection;350;0;173;0
WireConnection;490;0;496;1
WireConnection;490;1;540;0
WireConnection;22;0;480;0
WireConnection;22;1;13;0
WireConnection;483;0;484;0
WireConnection;483;1;485;0
WireConnection;464;0;389;0
WireConnection;464;2;453;0
WireConnection;500;1;22;0
WireConnection;500;0;490;0
WireConnection;482;0;8;0
WireConnection;482;2;483;0
WireConnection;454;0;464;0
WireConnection;446;0;189;1
WireConnection;446;1;189;2
WireConnection;9;0;22;0
WireConnection;9;1;482;0
WireConnection;9;2;500;0
WireConnection;9;3;375;0
WireConnection;455;0;454;0
WireConnection;447;1;446;0
WireConnection;332;0;9;0
WireConnection;457;0;455;1
WireConnection;457;1;455;0
WireConnection;371;0;369;0
WireConnection;371;1;368;0
WireConnection;393;0;447;0
WireConnection;372;0;480;0
WireConnection;372;1;371;0
WireConnection;458;0;457;0
WireConnection;458;1;456;0
WireConnection;459;0;458;0
WireConnection;373;0;372;0
WireConnection;373;1;374;0
WireConnection;373;2;511;0
WireConnection;412;0;405;0
WireConnection;460;0;454;0
WireConnection;463;0;460;0
WireConnection;463;1;459;0
WireConnection;503;0;189;3
WireConnection;503;1;189;4
WireConnection;344;0;412;0
WireConnection;344;1;373;0
WireConnection;465;0;463;0
WireConnection;465;1;462;0
WireConnection;465;2;461;0
WireConnection;501;0;503;0
WireConnection;383;0;382;0
WireConnection;383;1;380;0
WireConnection;123;1;344;0
WireConnection;450;0;307;3
WireConnection;125;0;123;0
WireConnection;466;1;389;0
WireConnection;466;0;465;0
WireConnection;387;0;480;0
WireConnection;387;1;383;0
WireConnection;392;0;387;0
WireConnection;392;1;466;0
WireConnection;392;2;441;0
WireConnection;448;0;132;0
WireConnection;448;1;449;0
WireConnection;124;0;125;0
WireConnection;47;1;392;0
WireConnection;310;0;311;0
WireConnection;310;1;448;0
WireConnection;126;0;124;0
WireConnection;126;1;127;0
WireConnection;512;1;47;1
WireConnection;512;0;47;4
WireConnection;190;0;126;0
WireConnection;190;1;310;0
WireConnection;396;0;512;0
WireConnection;147;0;144;0
WireConnection;130;0;190;0
WireConnection;1;1;424;0
WireConnection;142;0;130;0
WireConnection;142;2;147;0
WireConnection;27;0;1;4
WireConnection;27;1;25;4
WireConnection;27;2;29;4
WireConnection;27;3;397;0
WireConnection;345;0;142;0
WireConnection;275;1;27;0
WireConnection;275;0;27;0
WireConnection;30;0;29;0
WireConnection;2;0;1;0
WireConnection;28;0;25;0
WireConnection;326;0;275;0
WireConnection;326;1;346;0
WireConnection;26;0;2;0
WireConnection;26;1;28;0
WireConnection;26;2;30;0
WireConnection;315;0;326;0
WireConnection;0;2;26;0
WireConnection;0;9;315;0
ASEEND*/
//CHKSM=E7D7DF34CDF3F4E034D3533702392649BD58F64B