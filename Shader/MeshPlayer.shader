Shader "Motion/MeshPlayer" {
Properties {
	[Header(Texture)]
	[NoScaleOffset]
	_MainTex ("Albedo", 2D) = "white" {}
	_Color ("Color", Color) = (1,1,1,1)

	[Header(Clipping)]
	[Enum(UnityEngine.Rendering.CullMode)] _Cull("Face Culling", Float) = 2
	[Toggle(_ALPHATEST_ON)] _AlphaTest("Alpha Test", Float) = 0
	_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0
	_NearClip ("NearClip", Float) = 0

	[Header(Motion)]
	[NoScaleOffset] _Armature ("Armature", 2D) = "black" {}
	[NoScaleOffset] _MotionDec ("MotionDec", 2D) = "black" {}
	_Layer ("Layer", Float) = 0
	_RotationTolerance ("RotationTolerance", Range(0, 1)) = 0.1
}
SubShader {
	Tags { "Queue"="Geometry" "RenderType"="Opaque" }
	Pass {
		Tags { "LightMode"="ForwardBase" }
		Cull Off
CGPROGRAM
#pragma target 4.0
#pragma vertex vert
#pragma fragment frag
#pragma shader_feature _ALPHATEST_ON
#pragma multi_compile_instancing
#if defined(SHADER_API_GLES3)
	#define UNITY_COLORSPACE_GAMMA
#endif

#include <UnityCG.cginc>
#include <Lighting.cginc>

UNITY_INSTANCING_BUFFER_START(Props)
	UNITY_DEFINE_INSTANCED_PROP(float, _Layer)
UNITY_INSTANCING_BUFFER_END(Props)

#include "MeshPlayer.hlsl"
#include "Frag.hlsl"

void vert(VertInputPlayer i, out FragInput o) {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	bool highRange = true;
	#if defined(SHADER_API_GLES3)
		highRange = false;
	#endif
	VertInputSkinned I;
	SkinVertex(i, I, UNITY_ACCESS_INSTANCED_PROP(Props, _Layer), highRange);
	
	o.vertex = mul(unity_ObjectToWorld, float4(I.vertex, 1));
	o.normal = mul(unity_ObjectToWorld, float4(I.normal, 0));
	o.pos = UnityWorldToClipPos(o.vertex);
	o.tex = I.texcoord;
}
ENDCG
	}
}
}