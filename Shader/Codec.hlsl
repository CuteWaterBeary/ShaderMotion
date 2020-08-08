float3 unlerp(float3 a, float3 b, float3 y) {
	return (y-a)/(b-a);
}
float median(float3 v) {
	return clamp(v.x, min(v.y, v.z), max(v.y, v.z));
}
//// sRGB transfer function ////
float3 LinearToGamma(float3 color) {
	return color <= 0.0031308 ? 12.92 * color : 1.055 * pow(color, 1/2.4) - 0.055;
}
float3 GammaToLinear(float3 color) {
	return color <= 0.04045 ? color / 12.92 : pow(color/1.055 + 0.055/1.055, 2.4);
}
//// gray code codec ////
uint2 graycode_split(uint base, inout uint2 n) {
	uint2 d = n % base;
	n /= base;
	return (n & 1) ? base-1-d : d;
}
float3 graycode_expand(float f) {
	return float3(round(f), min(f-round(f), 0), max(f-round(f), 0));
}
float  graycode_extract(float3 e) {
	float2 d = e.yz-float2(-0.5,+0.5);
	return (d.x + d.y) / max(abs(d.x), abs(d.y)) * 0.5 + e.x;
}
float3 graycode_merge(uint base, float3 lo, float hi) {
	float rhi = round(hi);
	if(int(rhi) & 1)
		lo = float3(base-1,0,0)-lo.xzy;
	lo.yz += saturate((hi-rhi) * float2(-1, +1)) * (lo.x == float2(0, base-1) ? float2(-1, +1) : 0);
	lo.x  += rhi*base;
	return lo;
}
//// fixed point <-> RGB24 codec ////
void EncodeSigned(float x, out float3 c0, out float3 c1, out float3 c2, out float3 c3) {
	const uint base = 3, base6 = base*base*base*base*base*base;
	x = clamp((base6-1)/2 * x, -int(base6*base6-1)/2, +int(base6*base6-1)/2);
	uint2  n = int(floor(x)) + int2(0, 1) + int(base6*base6-1)/2;
	float2 wt = float2(1-frac(x), frac(x))/(base-1);
	c3.b = dot(graycode_split(base, n), wt);
	c3.r = dot(graycode_split(base, n), wt);
	c3.g = dot(graycode_split(base, n), wt);
	c2.b = dot(graycode_split(base, n), wt);
	c2.r = dot(graycode_split(base, n), wt);
	c2.g = dot(graycode_split(base, n), wt);
	c1.b = dot(graycode_split(base, n), wt);
	c1.r = dot(graycode_split(base, n), wt);
	c1.g = dot(graycode_split(base, n), wt);
	c0.b = dot(graycode_split(base, n), wt);
	c0.r = dot(graycode_split(base, n), wt);
	c0.g = dot(n, wt);
}
float DecodeSigned(float3 c0, float3 c1, float3 c2, float3 c3, bool hasInt=true) {
	const uint base=3;
	uint p = 1;
	c0 *= base-1, c1 *= base-1, c2 *= base-1, c3 *= base-1;
	float3 e = graycode_expand(c3.b);
	e = graycode_merge((p *= base), e, c3.r);
	e = graycode_merge((p *= base), e, c3.g);
	e = graycode_merge((p *= base), e, c2.b);
	e = graycode_merge((p *= base), e, c2.r);
	e = graycode_merge((p *= base), e, c2.g);
	if(hasInt) {
		e = graycode_merge((p *= base), e, c1.b);
		e = graycode_merge((p *= base), e, c1.r);
		e = graycode_merge((p *= base), e, c1.g);
		e = graycode_merge((p *= base), e, c0.b);
		e = graycode_merge((p *= base), e, c0.r);
		e = graycode_merge((p *= base), e, c0.g);
	}
	e.x -= ((p *= base)-1)/2;
	return graycode_extract(e) / ((pow(base,6)-1)/2);
}
float MergeSigned(float f0, float f1) {
	uint base = pow(3,6);
	float2 f = float2(f0, f1) * ((base-1)/2) + (base-1)/2;
	float3 e = graycode_expand(f.y);
	e = graycode_merge(base, e, f.x);
	e.x -= (base*base-1)/2;
	return graycode_extract(e) / ((base-1)/2);
}
//// slot index <-> rect codec ////
static uint2 entrySize = uint2(2, 1);
static uint2 matrixSize = uint2(80, 45) / entrySize;
float4 LocateSlot(uint idx) {
	uint2 pos = uint2(idx/uint(matrixSize.y), matrixSize.y-1-idx%uint(matrixSize.y));
	return float4(pos.xyxy + float4(0,0,1,1))/matrixSize.xyxy;
}
//// slot IO ////
SamplerState LinearClamp, PointClamp;
float3 RenderSlot(float3 c[2], float2 uv) {
	return GammaToLinear(c[dot(floor(uv * entrySize), 1)]);
}
float4 SampleSlot(Texture2D tex, float4 rect) {
	return tex.SampleLevel(PointClamp, lerp(rect.xy, rect.zw, 0.5), 0);
}
float SampleSlot_MergeSigned(Texture2D tex, float4 rect0, float4 rect1) {
	return MergeSigned(SampleSlot(tex, rect0).x, SampleSlot(tex, rect1).x);
}
float SampleSlot_DecodeSigned(Texture2D tex, float4 rect) {
	float3 c[2] = {
		LinearToGamma(tex.SampleLevel(LinearClamp, lerp(rect.xy, rect.zw, (min(1, entrySize)-0.5) / entrySize), 0)),
		LinearToGamma(tex.SampleLevel(LinearClamp, lerp(rect.xy, rect.zw, (min(2, entrySize)-0.5) / entrySize), 0)),
	};
	return DecodeSigned(0, 0, c[0], c[1], false);
}
float SampleSlot_DecodeSigned(Texture2D tex, float4 rect0, float4 rect1) {
	float3 c[4] = {
		LinearToGamma(tex.SampleLevel(LinearClamp, lerp(rect0.xy, rect0.zw, (min(1, entrySize)-0.5) / entrySize), 0)),
		LinearToGamma(tex.SampleLevel(LinearClamp, lerp(rect0.xy, rect0.zw, (min(2, entrySize)-0.5) / entrySize), 0)),
		LinearToGamma(tex.SampleLevel(LinearClamp, lerp(rect1.xy, rect1.zw, (min(1, entrySize)-0.5) / entrySize), 0)),
		LinearToGamma(tex.SampleLevel(LinearClamp, lerp(rect1.xy, rect1.zw, (min(2, entrySize)-0.5) / entrySize), 0)),
	};
	return DecodeSigned(c[0], c[1], c[2], c[3]);
}