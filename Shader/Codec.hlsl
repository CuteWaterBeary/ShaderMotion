float3 unlerp(float3 a, float3 b, float3 y) {
	return (y-a)/(b-a);
}
float median(float3 v) {
	return max(min(v.r, v.g), min(max(v.r, v.g), v.b));
}
float min3(float3 v) {
	return min(min(v.x, v.y), v.z);
}
float3 GammaToLinear(float3 color) {
	return color <= 0.04045F? color / 12.92F : pow((color + 0.055F)/1.055F, 2.4F);
}
float3 LinearToGamma(float3 color) {
	return color <= 0.0031308F ? 12.92F * color : 1.055F * pow(color, 0.4166667F) - 0.055F;
}

// static float3x3 RGB2YCC_BT709 = float3x3(
// 	float3( 0.2126,  0.7152,  0.0722),
// 	float3(-0.2126, -0.7152,  0.9278)/1.8556,
// 	float3( 0.7874, -0.7152, -0.0722)/1.5748);
// static float3x3 YCC2RGB_BT709 = float3x3(
// 	1,  0         ,  1.5748    ,
// 	1, -0.18732427, -0.46812427,
// 	1,  1.8556    ,  0         );

float3 encode6_grb_gray(uint n) {
	float3 c;
	uint3 d = (n >> uint3(4,2,0)) & 3;
	d.yz ^= (d.xy & 1) * 3;
	c.grb = d / 3.0;
	return c;
}
uint decode6_grb_gray(float3 c) {
	uint3 d = round(c.grb * 3);
	d.yz ^= (uint2(d.x, d.x^d.y) & 1) * 3;
	return dot(d, uint3(16,4,1));
}
float3 encode6_grb_hilbert(uint n) {
	float2 H4x4[16] = {
		{3,3},{3,2},{3,1},{3,0},{2,0},{2,1},{1,1},{1,0},
		{0,0},{0,1},{0,2},{0,3},{1,3},{1,2},{2,2},{2,3},
	};
	return (float3(n/16, H4x4[n%16]) / 3).grb;
}
uint decode6_grb_hilbert(float3 c) {
	uint4x4 H4x4 = uint4x4(8,9,10,11, 7,6,13,12, 4,5,14,15, 3,2,1,0);
	uint3 d = round(c.grb*3);
	return H4x4[d[1]][d[2]] + d[0]*16;
}
float3 encode6_grayscale(uint n) {
	return n/63.0;
}
uint decode6_grayscale(float3 c) {
	return round(dot(c, float3( 0.2126,  0.7152,  0.0722)) * 63);
}

#if defined(CODEC_HILBERT)
#define encode6 encode6_grb_hilbert
#define decode6 decode6_grb_hilbert
#elif defined(CODEC_GRAYSCALE)
#define encode6 encode6_grayscale
#define decode6 decode6_grayscale
#else
#define encode6 encode6_grb_gray
#define decode6 decode6_grb_gray
#endif

void EncodeUnorm(float x, out float3 c[2]) {
	uint n = min((uint)round(saturate(x) * (1<<12)), (1<<12)-1);
	uint2 d = uint2(n/64, n%64);
	d[1] ^= (d[0] & 1) * 63;
	c[0] = encode6(d[0]);
	c[1] = encode6(d[1]);
}
float DecodeUnorm(float3 c[2]) {
	uint2 d;
	d[0] = decode6(c[0]);
	d[1] = decode6(c[1]);
	d[1] ^= (d[0] & 1) * 63;
	return dot(d, rcp(float2(64, 64*64)));
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