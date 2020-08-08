Shader "Motion/Buffer" {
Properties {
	[NoScaleOffset] _Motion ("Motion", 2D) = "black" {}
	_FrameRate("FrameRate", Float) = 60
}
SubShader {
	Tags { "PreviewType"="Plane" }
	Pass {
		Lighting Off
		Blend One Zero
CGPROGRAM
#pragma target 5.0
#pragma vertex CustomRenderTextureVertexShader
#pragma fragment frag
#include "UnityCustomRenderTexture.cginc"
#include "Rotation.hlsl"
#include "Codec.hlsl"

Texture2D _Motion;
float _FrameRate;

float sampleSigned(float2 uv) {
	float4 rect = uv.xyxy + float2(-0.5,+0.5).xxyy/_CustomRenderTextureInfo.xyxy;
	if(uv.x > 0.5)
		rect.xz = rect.zx;
	return SampleSlot_DecodeSigned(_Motion, rect);
}
float4 frag(v2f_customrendertexture IN) : SV_Target {
	float2 uv = IN.globalTexcoord.xy;
	float2 uvT = float2(0.5, 0) + 0.5/_CustomRenderTextureInfo.xy;
	float prevT = tex2Dlod(_SelfTexture2D, float4(uvT,0,0)).x;
	float nextT = _Time.y;
	float deltaFrame = (nextT-prevT) * _FrameRate;

	float4 prev = tex2Dlod(_SelfTexture2D, float4(uv, 0, 0));
	float  next = sampleSigned(uv);
	if(all(abs(uv - uvT) < 1e-4))
		next = nextT;

	float2 uv0 = lerp(LocateSlot(1).xy, LocateSlot(1).zw, 0.5);
	bool frameChanged = tex2Dlod(_SelfTexture2D, float4(uv0, 0, 0)).x != sampleSigned(uv0);
	// use deltaFrame>1 to avoid jitter, and abs(deltaFrame)>2 to avoid freeze or time-wrap
	bool update = (frameChanged && deltaFrame > 1) || abs(deltaFrame) > 2;
	return update ? float4(next, prev.xyz) : prev;
}
ENDCG
	}
}
}