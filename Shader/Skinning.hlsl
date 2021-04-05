static const uint maxShapeInfluence = 16;
static const uint maxBoneInfluence = 4;
static const uint maxBoneDepth = 16; // standard humanoid only needs 11
void TransformBone(float4 data, inout float3x3 mat);
void TransformRoot(float4 data, inout float3x3 mat);
float2 GetBlendCoord(float4 data);

SamplerState LinearClampSampler;
struct VertInputSkin {
	float3 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float4 texcoord : TEXCOORD0;
	float4 boneWeights : TEXCOORD1;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
void MorphVertex(inout VertInputSkin i, Texture2D shapeTex) {
	uint2 size; shapeTex.GetDimensions(size.x, size.y);
	uint2 range = round(i.texcoord.zw); // z=length, w=start so that the default (0,0,0,1) has length 0
	uint2 loc0  = uint2(range.y%size.x, range.y/size.x);
	for(uint K=0; K<maxShapeInfluence; K++) {
		if(K >= range.x)
			break;
		uint2 loc = uint2(loc0.x+K*3, loc0.y);
		float2 coord = GetBlendCoord(shapeTex.Load(uint3(loc.xy, 0)));
		i.vertex += shapeTex.SampleLevel(LinearClampSampler, (loc + 0.5 + coord.xy) / size.xy, 0).xyz;
	}
}
void SkinVertex(inout VertInputSkin i, Texture2D boneTex) {
	float3 vertex = 0, normal = 0, tangent = 0;
	for(uint J=0; J<maxBoneInfluence; J++) {
		// bone + weight/2 == i.boneWeights[J]
		uint  bone  = floor(i.boneWeights[J]+0.25);
		float weight = frac(i.boneWeights[J]+0.25)*2-0.5;
		if(weight < 1e-4)
			break;

		float4 data4[1];
		float3x3 mat = transpose(float3x3(i.vertex.xyz, i.normal.xyz, i.tangent.xyz));
		for(uint I=0; I<4*maxBoneDepth; I+=4) {
			#if !defined(UNITY_COMPILER_HLSLCC)
				float4x4 data = transpose(float4x4(
					boneTex.Load(uint3(I, bone, 0), uint2(+0, 0)), boneTex.Load(uint3(I, bone, 0), uint2(+1, 0)),
					boneTex.Load(uint3(I, bone, 0), uint2(+2, 0)), boneTex.Load(uint3(I, bone, 0), uint2(+3, 0))));
			#else // hlslcc bug: texelFetchOffset generates ivec3(x, x)
				float4x4 data = transpose(float4x4(
					boneTex.Load(uint3(I+0, bone, 0)), boneTex.Load(uint3(I+1, bone, 0)),
					boneTex.Load(uint3(I+2, bone, 0)), boneTex.Load(uint3(I+3, bone, 0))));
			#endif
			mat = mul((float3x3)data, mat);
			mat._11_21_31 += data._14_24_34;
			if(data._44 < 0) {
				data4[0] = data._41_42_43_44; // writing to temp array prevents hlslcc turning for-loop into while
				break;
			}
			TransformBone(data._41_42_43_44, mat);
		}
		TransformRoot(data4[0], mat);
		vertex  += mat._11_21_31 * weight;
		normal  += mat._12_22_32 * weight;
		tangent += mat._13_23_33 * weight;
	}
	i.vertex.xyz = vertex;
	i.normal.xyz = normal;
	i.tangent.xyz = tangent;
}