Shader "Motion/MeshPlayer" {
Properties {
	[Header(Texture)]
	[NoScaleOffset]
	_MainTex ("MainTex", 2D) = "white" {}
	_Color ("Color", Color) = (1,1,1,1)

	[Header(Culling)]
	[Enum(UnityEngine.Rendering.CullMode)] _Cull("Face Culling", Float) = 2
	[Toggle(_ALPHATEST_ON)] _AlphaTest("Alpha Test", Float) = 0
	_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0

	[Header(Motion)]
	[NoScaleOffset] _MotionDec ("MotionDec (decoded motion texture)", 2D) = "black" {}
	[HideInInspector] _Bone ("Bone", 2D) = "black" {}
	[HideInInspector] _Shape ("Shape", 2D) = "black" {}
	[ToggleUI] _ApplyScale ("ApplyScale (resize avatar to fit encoded scale)", Float) = 1
	_Layer ("Layer (location of motion stripe)", Float) = 0
	_RotationTolerance ("RotationTolerance", Range(0, 1)) = 0.1
}
SubShader {
	Tags { "Queue"="Geometry" "RenderType"="Opaque" }
	Pass {
		Tags { "LightMode"="ForwardBase" }
		Cull [_Cull]
CGPROGRAM
#pragma target 4.0
#pragma vertex vert
#pragma fragment frag
#pragma shader_feature _ALPHATEST_ON
#pragma multi_compile_instancing

#include <UnityCG.cginc>
#include "MeshPlayer.hlsl"

UNITY_INSTANCING_BUFFER_START(Props)
	UNITY_DEFINE_INSTANCED_PROP(float, _Layer)
UNITY_INSTANCING_BUFFER_END(Props)

struct FragInput {
	half2  tex : TEXCOORD0;
	float3 vertex : TEXCOORD1;
	half3  normal : TEXCOORD2;
	float4 pos : SV_Position;
	UNITY_VERTEX_OUTPUT_STEREO
};

void vert(VertInputSkin i, out FragInput o) {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	SkinVertex(i, UNITY_ACCESS_INSTANCED_PROP(Props, _Layer));
	
	o.vertex = mul(unity_ObjectToWorld, float4(i.vertex, 1));
	o.normal = mul(unity_ObjectToWorld, float4(i.normal, 0));
	o.pos = UnityWorldToClipPos(o.vertex);
	o.tex = i.texcoord;
}

////////
// the following is a basic lit shader
// you should customize it to your needs

#include <Lighting.cginc>

sampler2D _MainTex;
half4 _Color;
half _Cutoff;

half4 frag(FragInput i) : SV_Target {
	half4 color = tex2D(_MainTex, i.tex);
	#if defined(_ALPHATEST_ON)
		if(color.a < _Cutoff)
			discard;
	#endif
	color *= _Color;

	half3 normal = normalize(i.normal);
	half ndl = dot(normal, float3(0,1,0));
	// shadow by saturation
	half3 shadow = lerp(color.rgb, 1, saturate(ndl+1));
	#if defined(SHADER_API_GLES3)
		color.rgb *= shadow;
		color.rgb = LinearToGamma(color.rgb);
		return color;
	#endif

	half3 light = _LightColor0.rgb + ShadeSH9(float4(0,1,0,1));
	light /= max(max(light.x, light.y), max(light.z, 1));
	// rim lighting
	half ndv = dot(normal, normalize(_WorldSpaceCameraPos - i.vertex));
	half rim = pow(1-abs(ndv), exp2(lerp(3,0,0.1)));
	rim = saturate(rim/0.074) * 0.2;
	color.rgb *= (rim*color.rgb+1) * shadow * light;
	return color;
}
ENDCG
	}
}
}