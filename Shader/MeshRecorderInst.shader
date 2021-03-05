Shader "Motion/MeshRecorderInst" {
Properties {
	[Header(Motion)]
	[ToggleUI] _AutoHide ("AutoHide (only visible in camera with farClip=0)", Float) = 1
	_Layer ("Layer (location of motion stripe)", Float) = 0
	_MainTex ("MainTex", 2D) = "black" {}
}
SubShader {
	Tags { "Queue"="Overlay" "RenderType"="Overlay" "PreviewType"="Plane" }
	Pass {
		Tags { "LightMode"="ForwardBase" }
		Cull Off
		ZTest Always ZWrite Off
CGPROGRAM
#pragma target 3.5
#pragma vertex vert
#pragma fragment fragTile
#pragma multi_compile_instancing

#include <UnityCG.cginc>
#include "MeshRecorder.hlsl"

Texture2D_float _MainTex;

struct VertInput {
	float3 vertex  : POSITION;
	float3 uv      : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
#if !defined(UNITY_INSTANCING_ENABLED)
void vert() {}
#else
void vert(VertInput i, out FragInputTile o) {
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	float4 data[8];
	UNITY_UNROLL for(uint J=0; J<8; J++)
		data[J] = _MainTex.Load(uint3(J, unity_InstanceID, 0));
	float4x4 mat0 = transpose(float4x4(data[0], data[1], data[2], data[3]));
	float4x4 mat1 = transpose(float4x4(data[4], data[5], data[6], data[7]));

	VertInputTile I;
	bool valid = i.uv.z < mat0[3].z;
	I.slot = mat0[3].x + i.uv.z;
	I.axis = mat0[3].y + i.uv.z;
	I.sign = mat1[3].xyz[I.axis];
	I.mat1 = mul(unity_ObjectToWorld, float4x4(mat1[0], mat1[1], mat1[2], float4(0,0,0,1)));
	unity_InstanceID = mat0[3].w;
	I.mat0 = mul(unity_ObjectToWorld, float4x4(mat0[0], mat0[1], mat0[2], float4(0,0,0,1)));
	
	o.uv = i.uv;
	float4 rect = EncodeTransform(I, o);
	o.pos.xy = valid ? lerp(rect.xy, rect.zw, o.uv) : 0;
}
#endif
ENDCG
	}
}
}