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
uint2 gray_decoder_pop(inout uint2 state, uint radix) {
	uint2 d = state % radix;
	state /= radix;
	return (state & 1) ? radix-1-d : d;
}
void  gray_encoder_add(inout float3 state, float x, uint radix, bool cont=true) {
	x = (int(state.x) & 1) ? radix-1-x : x;
	state.x  = state.x*radix + round(x);
	state.yz = cont && round(x) == float2(0, radix-1) ? state.yz : x-round(x);
}
float gray_encoder_sum(float3 state) {
	float2 p = max(0, state.zy * float2(+2, -2));
	return (min(p.x,p.y)/max(max(p.x,p.y)-p.x*p.y,1e-5)*(p.x-p.y)+(p.x-p.y)) * 0.5 + state.x;
}
//// real number <-> video color tile ////
static const uint ColorTileRadix = 3;
static const uint ColorTileLen = 2;
typedef half3 ColorTile[ColorTileLen];

static const uint tilePow = pow(ColorTileRadix, ColorTileLen*3);
void EncodeVideoSnorm(out ColorTile c, float x, bool hi=false) {
	x = clamp((tilePow-1)/2 * x, (tilePow*tilePow-1)/2 * -1.0, (tilePow*tilePow-1)/2);
	float2 wt = float2(1-frac(x), frac(x))/(ColorTileRadix-1);
	uint2 state = (int)floor(x) + int((tilePow*tilePow-1)/2) + int2(0, 1);
	{UNITY_UNROLL for(int i=int(ColorTileLen-1); i>=0; i--) {
		c[i].b = dot(gray_decoder_pop(state, ColorTileRadix), wt);
		c[i].r = dot(gray_decoder_pop(state, ColorTileRadix), wt);
		c[i].g = dot(gray_decoder_pop(state, ColorTileRadix), wt);
	}}
	if(hi)
		{UNITY_UNROLL for(int i=int(ColorTileLen-1); i>=0; i--) {
			c[i].b = dot(gray_decoder_pop(state, ColorTileRadix), wt);
			c[i].r = dot(gray_decoder_pop(state, ColorTileRadix), wt);
			c[i].g = dot(gray_decoder_pop(state, ColorTileRadix), wt);
		}}
}
float DecodeVideoSnorm(ColorTile c) {
	float3 state = 0;
	{UNITY_UNROLL for(int i=0; i<int(ColorTileLen); i++) {
		c[i] *= ColorTileRadix-1;
		gray_encoder_add(state, c[i].g, ColorTileRadix);
		gray_encoder_add(state, c[i].r, ColorTileRadix);
		gray_encoder_add(state, c[i].b, ColorTileRadix);
	}}
	return gray_encoder_sum(float3(state.x - (tilePow-1)/2, state.yz)) / ((tilePow-1)/2);
}
float DecodeVideoFloat(float hi, float lo) {
	float2 hilo = (tilePow-1)/2 + (tilePow-1)/2 * float2(hi, lo);
	float3 state = 0;
	gray_encoder_add(state, hilo.x, tilePow, false);
	gray_encoder_add(state, hilo.y, tilePow);
	return gray_encoder_sum(float3(state.x - (tilePow*tilePow-1)/2, state.yz)) / ((tilePow-1)/2);
}