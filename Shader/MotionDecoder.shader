Shader "Motion/MotionDecoder" {
Properties {
	// [NoScaleOffset] _Motion ("Motion", 2D) = "black" {}
}
SubShader {
	Tags { }
	Pass {
		Lighting Off
		Blend One Zero
CGPROGRAM
#pragma target 4.0
#pragma vertex vert
#pragma fragment frag
#if defined(SHADER_API_GLES3)
	#define UNITY_COLORSPACE_GAMMA
#endif

#include "UnityCustomRenderTexture.cginc"
#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "Layout.hlsl"

Texture2D _Motion;
float sampleSnorm(float2 uv) {
	float4 rect = uv.xyxy + float2(-0.5,+0.5).xxyy * fwidth(uv).xyxy;
	if(uv.x > 0.5)
		rect.xz = rect.zx;
	return SampleSlot_DecodeSnorm(_Motion, rect);
}
struct FragInput {
	float2 uv : TEXCOORD0;
	float4 pos : SV_Position;
};
void vert(appdata_customrendertexture i, out FragInput o) {
	// only use uv lookup table to keep it simple
	o.uv = CustomRenderTextureVertexShader(i).localTexcoord.xy;
	o.pos = float4(o.uv*2-1, 0, 1);
#if UNITY_UV_STARTS_AT_TOP
	o.pos.y *= -1;
#endif
}
float4 frag(FragInput i) : SV_Target {
	return BufferEncodeSnorm(sampleSnorm(i.uv));
}
ENDCG
	}
}
}