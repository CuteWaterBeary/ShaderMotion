Shader "Motion/Smooth" {
Properties {
	[NoScaleOffset] _MotionBuffer ("_MotionBuffer", 2D) = "black" {}
}
SubShader {
	Tags { "PreviewType"="Plane" }
	Pass {
		Lighting Off
		Blend One Zero
CGPROGRAM
#pragma vertex CustomRenderTextureVertexShader
#pragma fragment frag
#include "UnityCustomRenderTexture.cginc"
#include "Rotation.hlsl"
#include "Codec.hlsl"

Texture2D _MotionBuffer;
float frag(v2f_customrendertexture IN) : SV_Target {
	float4 sample  = SampleUnorm(_MotionBuffer, IN.globalTexcoord.xyxy);
	float4 sampleT = SampleUnorm(_MotionBuffer, float2(0.5, 0).xyxy + float4(0,0,1,1)/_CustomRenderTextureInfo.xyxy);

	float3 w = saturate((_Time.y-sampleT.xyz)/(sampleT.xyz-sampleT.yzw));
	w.yz = min(w.yz, 1-w.xy);

	return (
		+w.x * median(sample.xyz)
		+w.y * median(sample.yzw)
		+w.z * dot(sample.zw, 0.5))/(w.x+w.y+w.z);
}
ENDCG
	}
}
}