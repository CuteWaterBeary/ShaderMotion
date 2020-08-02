float3 unlerp(float3 a, float3 b, float3 y) {
	return (y-a)/(b-a);
}
float median(float3 v) {
	return clamp(v.x, min(v.y, v.z), max(v.y, v.z));
}

float3 GammaToLinear(float3 color) {
	return color <= 0.04045F? color / 12.92F : pow((color + 0.055F)/1.055F, 2.4F);
}
float3 LinearToGamma(float3 color) {
	return color <= 0.0031308F ? 12.92F * color : 1.055F * pow(color, 0.4166667F) - 0.055F;
}

uint2 graycode_encode(uint base, inout uint2 n) {
	uint2 d = n % base;
	n /= base;
	return (n & 1) ? base-1-d : d;
}
float graycode_decode(uint base, float lo, float hi) {
	float ce = ceil(hi);
	// flip lo so that it's 0 at U-turn
	float2 ST = (int(ce) & 1) ? float2(-1, base-1) : float2(1, 0);
	lo = lo * ST.x + ST.y;
	// locate nearest curve point
	float2 P = 0.5 - float2(lo, ce-hi);
	P = 0.5 - P / max(P.x, abs(P.y)) * 0.5;
	lo = min(lo, P.x);
	// bilinear interpolation
	return lo + (base-1-2*lo) * P.y + base * (ce-P.y);
}
void EncodeUnorm(float x, out float3 c[2], uint base=3) {
	x = saturate(x) * (pow(base,6)-1);
	uint2  n = uint(floor(x)) + uint2(0, 1);
	float2 wt = float2(1-frac(x), frac(x));
	float3 d0, d1;
	d1[2] = dot(graycode_encode(base, n), wt);
	d1[1] = dot(graycode_encode(base, n), wt);
	d1[0] = dot(graycode_encode(base, n), wt);
	d0[2] = dot(graycode_encode(base, n), wt);
	d0[1] = dot(graycode_encode(base, n), wt);
	d0[0] = dot(n, wt);
	c[0].grb = d0/(base-1);
	c[1].grb = d1/(base-1);
}
float DecodeUnorm(float3 c[2], uint base=3) {
	float3 d0 = c[0].grb * (base-1);
	float3 d1 = c[1].grb * (base-1);
	float v = d1[2];
	v = graycode_decode(pow(base,1), v, d1[1]);
	v = graycode_decode(pow(base,2), v, d1[0]);
	v = graycode_decode(pow(base,3), v, d0[2]);
	v = graycode_decode(pow(base,4), v, d0[1]);
	v = graycode_decode(pow(base,5), v, d0[0]);
	return v / (pow(base,6)-1);
}

static uint2 entrySize = uint2(2, 1);
static uint2 matrixSize = uint2(80, 45) / entrySize;
float4 GetRect(uint idx) {
	uint2 pos = uint2(idx/uint(matrixSize.y), matrixSize.y-1-idx%uint(matrixSize.y));
	return float4(pos.xyxy + float4(0,0,1,1))/matrixSize.xyxy;
}

SamplerState LinearClamp, PointClamp;
float4 SampleUnorm(Texture2D tex, float4 rect) {
	return tex.SampleLevel(PointClamp, lerp(rect.xy, rect.zw, 0.5), 0);
}
float SampleUnormDecode(Texture2D tex, float4 rect) {
	float3 colors[2] = {
		LinearToGamma(tex.SampleLevel(LinearClamp, lerp(rect.xy, rect.zw, (min(1, entrySize)-0.5) / entrySize), 0)),
		LinearToGamma(tex.SampleLevel(LinearClamp, lerp(rect.xy, rect.zw, (min(2, entrySize)-0.5) / entrySize), 0)),
	};
	return DecodeUnorm(colors);
}
float3 OutputUnorm(float3 c[2], float2 uv) {
	return GammaToLinear(c[dot(floor(uv * entrySize), 1)]);
}
// float3 OutputUnorm(float3 c[2], float2 uv, float2 uv0) {
// 	return OutputUnorm(c, uv);
// }