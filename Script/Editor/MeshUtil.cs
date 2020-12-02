using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ShaderMotion {
public class MeshUtil {
	static (int index, float weight)[] UnpackBoneWeight(BoneWeight bw) {
		return new[]{
			(bw.boneIndex0, bw.weight0),
			(bw.boneIndex1, bw.weight1),
			(bw.boneIndex2, bw.weight2),
			(bw.boneIndex3, bw.weight3)};
	}
	static BoneWeight PackBoneWeight((int index, float weight)[] bw) {
		return new BoneWeight{
			boneIndex0 = bw[0].index, weight0 = bw[0].weight,
			boneIndex1 = bw[1].index, weight1 = bw[1].weight,
			boneIndex2 = bw[2].index, weight2 = bw[2].weight,
			boneIndex3 = bw[3].index, weight3 = bw[3].weight};
	}
	static int[] RetargetBones(Transform[] srcBones, Transform[] dstBones) {
		var boneMap = Enumerable.Repeat(-1, srcBones.Length).ToArray();
		for(int i=0; i<srcBones.Length; i++)
			for(var b = srcBones[i]; b != null && boneMap[i] < 0; b = b.parent)
				boneMap[i] = System.Array.LastIndexOf(dstBones, b);
		return boneMap;
	}
	static void RetargetBindposes(Transform[] srcBones, Transform[] dstBones,
									Matrix4x4[] srcBindposes, Matrix4x4[] dstBindposes, int[] boneMap) {
		for(int k=0; k<2; k++)
		for(int i=0; i<srcBones.Length; i++) {
			var j = boneMap[i];
			if(j >= 0 && dstBindposes[j][3,3] == 0)
				if(k == 1) {
					dstBindposes[j] = (dstBones[j].worldToLocalMatrix * srcBones[i].localToWorldMatrix) * srcBindposes[i];
					Debug.Log($"Indirect retarget: bindpose[{(HumanBodyBones)j}] = MAT * bindpose[{srcBones[i]}]");
				} else if(dstBones[j] == srcBones[i])
					dstBindposes[j] = srcBindposes[i];
		}
	}
	static Matrix4x4[] RetargetBoneWeights(Transform[] srcBones, Transform[] dstBones,
											Matrix4x4[] srcBindposes, Matrix4x4[] dstBindposes,
											BoneWeight[] boneWeights, int[] boneMap) {
		Debug.Assert(srcBones.Length == srcBindposes.Length && dstBones.Length == dstBindposes.Length);
		var transforms = new Matrix4x4[boneWeights.Length];
		for(int v=0; v<boneWeights.Length; v++) {
			var bw = UnpackBoneWeight(boneWeights[v]);
			var weights = new float[dstBones.Length];
			var srcMatSum = new Matrix4x4();
			var dstMatSum = new Matrix4x4();
			foreach(var (i, wt) in bw) {
				var j = boneMap[i];
				if(wt != 0) {
					var srcMat = srcBones[i].localToWorldMatrix * srcBindposes[i];
					var dstMat = dstBones[j].localToWorldMatrix * dstBindposes[j];
					for(int k=0; k<16; k++) {
						srcMatSum[k] += srcMat[k] * wt;
						dstMatSum[k] += dstMat[k] * wt;
					}
					weights[j] += wt;
				}
			}

			if(srcMatSum != dstMatSum) {
				var diffm = dstMatSum.inverse * srcMatSum;
				var diffv = + (diffm.GetColumn(0) - new Vector4(1,0,0,0)).sqrMagnitude
							+ (diffm.GetColumn(1) - new Vector4(0,1,0,0)).sqrMagnitude
							+ (diffm.GetColumn(2) - new Vector4(0,0,1,0)).sqrMagnitude
							+ (diffm.GetColumn(3) - new Vector4(0,0,0,1)).sqrMagnitude;
				if(diffv > 1e-8)
					Debug.Log($"Transform is not identity: vertex affected by {srcBones[boneWeights[v].boneIndex0]} and {srcBones[boneWeights[v].boneIndex1]}");
			}

			System.Array.Clear(bw, 0, bw.Length);
			var idx = 0;
			foreach(var dstBone in Enumerable.Range(0, dstBones.Length).OrderBy(i => -weights[i]).Take(4))
				bw[idx++] = (dstBone, weights[dstBone]);

			transforms[v] = srcMatSum == dstMatSum ? Matrix4x4.identity : dstMatSum.inverse * srcMatSum;
			boneWeights[v] = PackBoneWeight(bw);
		}
		return transforms;
	}
	public static Matrix4x4[] RetargetBindWeights(Transform[] srcBones, Transform[] dstBones,
													Matrix4x4[] srcBindposes, Matrix4x4[] dstBindposes,
													BoneWeight[] boneWeights) {
		var boneMap = RetargetBones(srcBones, dstBones);
		// unmap unused srcBones
		var used = new bool[srcBones.Length];
		foreach(var bw in boneWeights)
			foreach(var (index, weight) in UnpackBoneWeight(bw))
				if(weight != 0)
					used[index] = true;
		for(int i=0; i<srcBones.Length; i++)
			if(!used[i])
				boneMap[i] = -1;
		RetargetBindposes(srcBones, dstBones, srcBindposes, dstBindposes, boneMap);
		// map unmapped srcBones
		var defaultBoneIndex = boneMap.Where(x => x >= 0).FirstOrDefault();
		for(int i=0; i<boneMap.Length; i++)
			if(boneMap[i] < 0 && used[i]) {
				boneMap[i] = defaultBoneIndex;
				Debug.Log($"Default retarget: {srcBones[i]} => {(HumanBodyBones)defaultBoneIndex}");
			}
		return RetargetBoneWeights(srcBones, dstBones, srcBindposes, dstBindposes, boneWeights, boneMap);
	}
}
}