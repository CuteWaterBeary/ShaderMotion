Shader "Motion/DataPlayer" {
Properties {
	[Header(Motion)]
	[NoScaleOffset] _MotionDec ("MotionDec (decoded motion texture)", 2D) = "black" {}
	[HideInInspector] _Bone ("Bone", 2D) = "black" {}
	[HideInInspector] _Shape ("Shape", 2D) = "black" {}
	_HumanScale ("HumanScale (hips height: 0=original, -1=encoded)", Float) = 0
	_Layer ("Layer (location of motion stripe)", Float) = 0
	_RotationTolerance ("RotationTolerance", Range(0, 1)) = 0.1
	[ToggleUI] _AutoHide ("AutoHide (only visible in camera with farClip=0)", Float) = 1
}
SubShader {
	Tags { "Queue"="Overlay" "RenderType"="Overlay" "PreviewType"="Plane" }
	Pass {
		Tags { "LightMode"="ForwardBase" }
		Cull Off
		ZTest Always ZWrite Off
CGPROGRAM
#pragma target 4.0
#pragma vertex vert
#pragma fragment frag
#pragma geometry geom
#pragma multi_compile_instancing

#include <UnityCG.cginc>
#include "MeshPlayer.hlsl"

float _AutoHide;
UNITY_INSTANCING_BUFFER_START(Props)
	UNITY_DEFINE_INSTANCED_PROP(float, _Layer)
UNITY_INSTANCING_BUFFER_END(Props)

struct GeomInput {
	float3 vertex : TEXCOORD0;
	float3 normal : TEXCOORD1;
	float4 tangent : TEXCOORD2;
	float4 texcoord : TEXCOORD3;
	float4 boneWeights : TEXCOORD4;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct FragInput {
	float2 uv : TEXCOORD0;
	float4 pos : SV_Position;
	nointerpolation float4x4 mat0 : TEXCOORD1;
	UNITY_VERTEX_OUTPUT_STEREO
};

void vert(VertInputSkin i, out GeomInput o) {
	UNITY_SETUP_INSTANCE_ID(i);
	MorphAndSkinVertex(i, UNITY_ACCESS_INSTANCED_PROP(Props, _Layer));
	o = i;
}
float4x4 getMatrix(GeomInput i) {
	float4x4 m;
	m.c0 = cross(normalize(i.normal), i.tangent.xyz);
	m.c1 = i.normal;
	m.c2 = i.tangent.xyz;
	m.c3 = i.vertex;
	m._41_42_43_44 = float4(0,0,0,1);
	return m;
}
float4 getDataRect(GeomInput i) {
	float idx = i.texcoord.x;
	float layer = UNITY_ACCESS_INSTANCED_PROP(Props, _Layer);
	float4 rect = float4(idx,layer*4+0, idx+1,layer*4+4);
	rect /= abs(_ScreenParams.xyxy);
	rect = rect*2-1;
	rect.yw *= _ProjectionParams.x;
	#if defined(USING_STEREO_MATRICES)
		return 0; // hide in VR
	#endif
	if(any(UNITY_MATRIX_P[2].xy))
		return 0; // hide in mirror (near plane normal != Z axis)
	if(_AutoHide && _ProjectionParams.z != 0)
		return 0;
	return rect;
}
[maxvertexcount(4)]
void geom(point GeomInput i[1], inout TriangleStream<FragInput> stream) {
	FragInput o;
	UNITY_SETUP_INSTANCE_ID(i[0]);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	float4 rect = getDataRect(i[0]);
	float4 uv = float4(0,0,1,1);
	o.mat0 = getMatrix(i[0]);
	o.pos = float4(0, 0, UNITY_NEAR_CLIP_VALUE, 1);
	o.uv = uv.xy, o.pos.xy = rect.xy, stream.Append(o);
	o.uv = uv.xw, o.pos.xy = rect.xw, stream.Append(o);
	o.uv = uv.zy, o.pos.xy = rect.zy, stream.Append(o);
	o.uv = uv.zw, o.pos.xy = rect.zw, stream.Append(o);
}
half4 frag(FragInput i) : SV_Target {
	switch(floor(i.uv.y*4)) {
	case 0: return transpose(i.mat0)[0];
	case 1: return transpose(i.mat0)[1];
	case 2: return transpose(i.mat0)[2];
	case 3: return transpose(i.mat0)[3];
	default: return 0;
	}
}
ENDCG
	}
}
}