sampler2D _MainTex;
float4 _Color;
float _Cutoff;
float _NearClip;

struct FragInput {
	float2 tex : TEXCOORD0;
	float3 vertex : TEXCOORD1;
	float3 normal : TEXCOORD2;
	float4 pos : SV_Position;
	UNITY_VERTEX_OUTPUT_STEREO
};

float4 frag(FragInput i) : SV_Target {
	float4 color = tex2D(_MainTex, i.tex) * _Color;
	float3 normal = normalize(i.normal);
	float ndl = dot(normal, float3(0,1,0));
	float3 shadow = lerp(color.rgb, 1, saturate(ndl+1));
#if SHADER_API_MOBILE
	return float4(color.rgb * shadow, 1);
#endif
#ifdef _ALPHATEST_ON
	if(color.a <= _Cutoff)
		discard;
	if(length(_WorldSpaceCameraPos-i.vertex) < _NearClip)
		discard;
#endif
	float3 ambient = _LightColor0.rgb + ShadeSH9(float4(0,1,0,1));
	ambient /= max(max(ambient.x, ambient.y), max(ambient.z, 1));

	float ndv = dot(normal, normalize(_WorldSpaceCameraPos-i.vertex));
	float rim = pow(1-abs(ndv), exp2(lerp(3,0,0.1)));
	rim = saturate(rim/0.074) * 0.2;

	return float4((rim*color.rgb+1) * color.rgb * shadow * ambient, color.a);
}