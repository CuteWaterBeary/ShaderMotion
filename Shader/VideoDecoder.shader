Shader "Motion/VideoDecoder" {
Properties {
	_Motion ("Motion", 2D) = "black" {}
}
SubShader {
	Pass {
		Lighting Off
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
#include "VideoLayout.hlsl"

Texture2D _Motion;
float4 _Motion_ST;
float sampleSnorm(float2 uv) {
	float4 rect = GetTileRect(uv);
	if(uv.x > 0.5)
		rect.xz = rect.zx;
	ColorTile c;
	SampleTile(c, _Motion, rect * _Motion_ST.xyxy + _Motion_ST.zwzw);
	return DecodeVideoSnorm(c);
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
	return EncodeBufferSnorm(sampleSnorm(i.uv));
}
ENDCG
	}
}
}