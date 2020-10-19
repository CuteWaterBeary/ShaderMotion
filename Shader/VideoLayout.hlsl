//// tile index <-> uv ////
static const uint2 VideoResolution = uint2(80, 45);

static const uint2 tileCount = VideoResolution / uint2(ColorTileLen, 1);
static const float4 tileAABB = (float2(0,1).xxyy + 0.5) / tileCount.xyxy * float2(1,-1).xyxy + float2(0,1).xyxy;
float4 GetTileRect(float2 uv) {
	return (floor(uv * tileCount).xyxy + float4(0,0,1,1)) / tileCount.xyxy;
}
float4 GetTileRect(uint idx) {
	return lerp(tileAABB.xyxy, tileAABB.zwzw, float2(idx/tileCount.y, idx%tileCount.y).xyxy + float2(-0.5,+0.5).xxyy);
}
float4 GetTileX(uint4 idx) {
	return lerp(tileAABB.x, tileAABB.z, idx/tileCount.y);
}
float4 GetTileY(uint4 idx) {
	return lerp(tileAABB.y, tileAABB.w, idx%tileCount.y);
}
static float4 layerRect = float4(0, 0, GetTileRect(134).z, 1);
//// tile uv <-> color ////
SamplerState LinearClamp, PointClamp;
float4 RenderTile(ColorTile c, float2 uv) {
	return float4(DecodeGamma(c[floor(saturate(uv.x) * ColorTileLen)]), 1);
}
void SampleTile(out ColorTile c, Texture2D_half tex, float4 rect) {
	UNITY_UNROLL for(int i=0; i<int(ColorTileLen); i++)
		c[i] = EncodeGamma((half3)tex.SampleLevel(LinearClamp, lerp(rect.xy, rect.zw, float2((i+0.5)/ColorTileLen, 0.5)), 0));
}