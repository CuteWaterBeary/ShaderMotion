//// slot index <-> rect ////
static uint2 entrySize = uint2(2, 1);
static uint2 matrixSize = uint2(80, 45) / entrySize;
static float4 slotAABB = (float2(0,1).xxyy + 0.5) / matrixSize.xyxy * float2(1,-1).xyxy + float2(0,1).xyxy;
float4 GetSlotRect(uint idx) {
	return lerp(slotAABB.xyxy, slotAABB.zwzw, float2(idx/matrixSize.y, idx%matrixSize.y).xyxy + float2(-0.5,+0.5).xxyy);
}
float4 GetSlotX(uint4 idx) {
	return lerp(slotAABB.x, slotAABB.z, idx/matrixSize.y);
}
float4 GetSlotY(uint4 idx) {
	return lerp(slotAABB.y, slotAABB.w, idx%matrixSize.y);
}
//// slot codec ////
SamplerState LinearClamp, PointClamp;
float3 RenderSlot(float3 c[2], float2 uv) {
	return GammaToLinear(c[dot(floor(uv * entrySize), 1)]);
}
float SampleSlot_DecodeSnorm(Texture2D_half tex, float4 rect) {
	half3 c[2] = {
		LinearToGamma((half3)tex.SampleLevel(LinearClamp, lerp(rect.xy, rect.zw, (min(1, entrySize)-0.5) / entrySize), 0)),
		LinearToGamma((half3)tex.SampleLevel(LinearClamp, lerp(rect.xy, rect.zw, (min(2, entrySize)-0.5) / entrySize), 0)),
	};
	return VideoDecodeSnorm(c[0], c[1]);
}