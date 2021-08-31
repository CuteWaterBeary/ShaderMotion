//// tile index <-> uv ////
static const uint2 VideoResolution = uint2(80, 45);

static const uint2 tileCount = VideoResolution / uint2(ColorTileLen, 1);
static const float4 tileST = {float2(1,-1) / tileCount, float2(0,1)};
float4 GetTileRect(float2 uv) {
	return (floor(uv * tileCount).xyxy + float4(0,0,1,1)) / tileCount.xyxy;
}
float4 GetTileRect(uint idx) {
	return tileST.zwzw + float4(0,0,tileST.xy) + tileST.xyxy*float2(idx/tileCount.y, idx%tileCount.y).xyxy;
}
float4 GetTileX(uint4 idx) {
	return tileST.z + tileST.x*0.5 + tileST.x*(idx/tileCount.y);
}
float4 GetTileY(uint4 idx) {
	return tileST.w + tileST.y*0.5 + tileST.y*(idx%tileCount.y);
}
static float4 layerRect = float4(0, 0, GetTileRect(134).z, 1);
//// tile uv <-> color ////
SamplerState LinearClamp, PointClamp;
half4 RenderTile(ColorTile c, float2 uv) {
	half3 color = uv.x < 0.5 ? c[0] : c[ColorTileLen-1]; // avoid dynamic indexing on varying since it breaks on AMD
	#if !defined(SHADER_API_WEBGL)
		color = GammaToLinear(color);
	#endif
	return half4(color, 1);
}
void SampleTile(out ColorTile c, Texture2D_half tex, float4 rect, bool sampleGamma=false) {
	UNITY_UNROLL for(int i=0; i<int(ColorTileLen); i++) {
		c[i] = tex.SampleLevel(LinearClamp, lerp(rect.xy, rect.zw, float2((i+0.5)/ColorTileLen, 0.5)), 0);
		#if !defined(SHADER_API_WEBGL)
		if(!sampleGamma)
			c[i] = LinearToGamma(c[i]);
		#endif
	}
}