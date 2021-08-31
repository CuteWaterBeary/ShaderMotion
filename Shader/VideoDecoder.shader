Shader "Motion/VideoDecoder" {
Properties {
	_MainTex ("MainTex (motion video texture)", 2D) = "black" {}
	_FrameRate ("FrameRate (interpolation fps)", Float) = 0 // 0=disable
	[ToggleUI] _ApplyGamma("ApplyGamma", Float) = 0
}
SubShader {
	Pass {
CGINCLUDE
#pragma target 3.5
#pragma vertex vert
#pragma fragment frag
#pragma shader_feature SHADER_API_WEBGL

#include "UnityCustomRenderTexture.cginc"
#include "Rotation.hlsl"
#include "Codec.hlsl"
#include "VideoLayout.hlsl"

void vert(appdata_customrendertexture i, out float2 texcoord : TEXCOORD0, out float4 vertex : SV_Position) {
	texcoord = CustomRenderTextureVertexShader(i).localTexcoord.xy; // lite version
	vertex = float4(texcoord*2-1, UNITY_NEAR_CLIP_VALUE, 1);
#if UNITY_UV_STARTS_AT_TOP
	vertex.y *= -1;
#endif
}

Texture2D _MainTex;
float4 _MainTex_ST;
float _ApplyGamma;
float sampleSnorm(float2 uv) {
	float4 rect = GetTileRect(uv);
	if(uv.x > 0.5)
		rect.xz = rect.zx;
	ColorTile c;
	SampleTile(c, _MainTex, rect * _MainTex_ST.xyxy + _MainTex_ST.zwzw, _ApplyGamma);
	return DecodeVideoSnorm(c);
}

float _FrameRate;
float4 frag(float2 texcoord : TEXCOORD0) : SV_Target {
	float v = sampleSnorm(texcoord);
#if defined(SHADER_API_WEBGL)
	return EncodeBufferSnorm(v);
#else
	float3 buf = tex2Dlod(_SelfTexture2D, float4(texcoord, 0, 0)).yzw;
	if(buf.y != v)
		buf = float3(buf.y, v, _Time.y);
	if(_FrameRate)
		v = lerp(buf.x, buf.y, saturate((_Time.y-buf.z + unity_DeltaTime.z)*_FrameRate));
	return float4(v, buf);
#endif
}
ENDCG
CGPROGRAM
ENDCG
	}
	Pass { // no-op pass, used to populate double buffered CRT
		ColorMask 0
CGPROGRAM
ENDCG
	}
}
}