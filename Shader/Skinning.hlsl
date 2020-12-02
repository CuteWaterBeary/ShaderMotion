void TransformBone(inout float3x3 mat, float4 data);
void TransformRoot(inout float3x3 mat, float4 data);
float GetShapeWeight(float data);

struct VertInputSkin {
	float3 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float4 texcoord : TEXCOORD0;
	float4 boneWeights : TEXCOORD1;
	UNITY_VERTEX_INPUT_INSTANCE_ID

	uint3 GetShapeLocation(uint2 size) {
		uint2 range = round(texcoord.zw); // fix precision issue
		return uint3(range.x%size.x, range.x/size.x, range.y);
	}
	bool GetBoneWeight(uint idx, out uint bone, out float weight) {
		float bw = boneWeights[idx], b = floor(bw+0.25); // fix precision issue
		bone = b, weight = (bw-b)*2;
		return weight > 1e-4;
	}
};
void SkinVertex(inout VertInputSkin i, Texture2D boneTex, Texture2D shapeTex) {
	// morphing
	uint2 size; shapeTex.GetDimensions(size.x, size.y);
	uint3 shape = i.GetShapeLocation(size);
	for(uint K=0; K<16; K++) {
		if(K >= shape.z)
			break;
		float4 dv = shapeTex.Load(uint3(shape.xy, 0));
		shape.x ++;
		i.vertex.xyz += dv.xyz * GetShapeWeight(dv.w);
	}
	// rigging
	float3 vertex = 0, normal = 0, tangent = 0;
	for(uint J=0; J<4; J++) {
		uint bone; float weight;
		if(!i.GetBoneWeight(J, bone, weight))
			break;

		float4 data4[1];
		float3x3 mat = transpose(float3x3(i.vertex.xyz, i.normal.xyz, i.tangent.xyz));
		for(uint I=0; I<64; I+=4) {
			float4x4 data = transpose(float4x4(
				boneTex.Load(uint3(I, bone, 0), uint2(+0, 0)),
				boneTex.Load(uint3(I, bone, 0), uint2(+1, 0)),
				boneTex.Load(uint3(I, bone, 0), uint2(+2, 0)),
				boneTex.Load(uint3(I, bone, 0), uint2(+3, 0))));
			mat = mul((float3x3)data, mat);
			mat._11_21_31 += data._14_24_34;
			if(data._44 < 0) {
				data4[0] = data._41_42_43_44; // writing to temp array prevents corrupting for-loop
				break;
			}
			TransformBone(mat, data._41_42_43_44);
		}
		TransformRoot(mat, data4[0]);
		vertex  += mat._11_21_31 * weight;
		normal  += mat._12_22_32 * weight;
		tangent += mat._13_23_33 * weight;
	}
	i.vertex.xyz = vertex;
	i.normal.xyz = normal;
	i.tangent.xyz = tangent;
}