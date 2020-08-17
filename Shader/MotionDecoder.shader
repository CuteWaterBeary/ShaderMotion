Shader "Motion/Decoder" {
Properties {
	[NoScaleOffset] _Motion ("Motion", 2D) = "black" {}
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

float sampleSigned(float2 uv) {
	float4 rect = uv.xyxy + float2(-0.5,+0.5).xxyy/_CustomRenderTextureInfo.xyxy;
	if(uv.x > 0.5)
		rect.xz = rect.zx;
	return SampleSlot_DecodeSigned(_Motion, rect, rect, false);
}
float4 frag(v2f_customrendertexture IN) : SV_Target {
	float2 uv = IN.globalTexcoord.xy;
	return sampleSigned(uv);
}
ENDCG
	}
}
}