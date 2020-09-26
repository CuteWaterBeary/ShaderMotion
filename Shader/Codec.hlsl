//// sRGB linear color <-> sRGB gamma color ////
half3 EncodeGamma(half3 color) {
	return color <= 0.0031308 ? 12.92 * color : 1.055 * pow(color, 1/2.4) - 0.055;
}
half3 DecodeGamma(half3 color) {
	return color <= 0.04045 ? color / 12.92 : pow(color/1.055 + 0.055/1.055, 2.4);
}
#if defined(UNITY_COLORSPACE_GAMMA)
#define EncodeGamma(x) (x)
#define DecodeGamma(x) (x)
#endif
//// real number <-> render texture color ////
half4 EncodeBufferSnorm(float x) {
	float4 scale = 0.25 * (1 << uint4(0, 8, 16, 24));
	float4 v = frac(x * scale + scale);
	v.xyz -= v.yzw / (1 << 8);
	return v / (255.0/256);
}
float DecodeBufferSnorm(half4 v) {
	float4 scale = (255.0/256) / (1 << uint4(0, 8, 16, 24)) * 4;
	return dot(v, scale) - 1;
}
#if !defined(SHADER_API_MOBILE)
#define EncodeBufferSnorm(x) ((x).rrrr)
#define DecodeBufferSnorm(x) ((x).r)
#endif
//// real number <-> Gray curve coordinates ////
uint2 gray_decoder_pop(inout uint2 n, uint base) {
	uint2 d = n % base;
	n /= base;
	return (n & 1) ? base-1-d : d;
}
void gray_encoder_new(out float3 state, float x) {
	state = float3(round(x), min(x-round(x), 0), max(x-round(x), 0));
}
void gray_encoder_add(inout float3 state, float x, uint range) {
	float r = round(x);
	state.xyz = (int(r) & 1) ? float3(range-1,0,0)-state.xzy : state.xyz;
	state.yz += saturate((x-r) * float2(-1, +1)) * (state.x == float2(0, range-1) ? float2(-1, +1) : 0);
	state.x  += r*range;
}
float gray_encoder_sum(float3 state) {
	state.yz -= float2(-0.5,+0.5);
	return (state.y + state.z) / max(abs(state.y), abs(state.z)) * 0.5 + state.x;
}
//// real number <-> video colors ////
void EncodeVideoFloat(float x, out half3 hi0, out half3 hi1, out half3 lo0, out half3 lo1) {
	const uint base = 3, base6 = base*base*base*base*base*base;
	x = clamp((base6-1)/2 * x, -int(base6*base6-1)/2, +int(base6*base6-1)/2);
	uint2  n = int(floor(x)) + int2(0, 1) + int(base6*base6-1)/2;
	float2 wt = float2(1-frac(x), frac(x))/(base-1);
	lo1.b = dot(gray_decoder_pop(n, base), wt);
	lo1.r = dot(gray_decoder_pop(n, base), wt);
	lo1.g = dot(gray_decoder_pop(n, base), wt);
	lo0.b = dot(gray_decoder_pop(n, base), wt);
	lo0.r = dot(gray_decoder_pop(n, base), wt);
	lo0.g = dot(gray_decoder_pop(n, base), wt);
	hi1.b = dot(gray_decoder_pop(n, base), wt);
	hi1.r = dot(gray_decoder_pop(n, base), wt);
	hi1.g = dot(gray_decoder_pop(n, base), wt);
	hi0.b = dot(gray_decoder_pop(n, base), wt);
	hi0.r = dot(gray_decoder_pop(n, base), wt);
	hi0.g = dot(n, wt);
}
float DecodeVideoSnorm(half3 lo0, half3 lo1) {
	const uint base = 3, base6 = base*base*base*base*base*base;
	uint p = 1;
	lo0 *= base-1, lo1 *= base-1;
	float3 state;
	gray_encoder_new(state, lo1.b);
	gray_encoder_add(state, lo1.r, (p *= base));
	gray_encoder_add(state, lo1.g, (p *= base));
	gray_encoder_add(state, lo0.b, (p *= base));
	gray_encoder_add(state, lo0.r, (p *= base));
	gray_encoder_add(state, lo0.g, (p *= base));
	state.x -= (base6-1)/2;
	return gray_encoder_sum(state) / ((base6-1)/2);
}
float DecodeVideoFloat(float hi, float lo) {
	const uint base = pow(3,6);
	float2 d = float2(hi, lo) * ((base-1)/2) + (base-1)/2;
	float3 state;
	gray_encoder_new(state, d[1]);
	gray_encoder_add(state, d[0], base);
	state.x -= (base*base-1)/2;
	return gray_encoder_sum(state) / ((base-1)/2);
}