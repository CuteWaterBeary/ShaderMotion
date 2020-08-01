using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using UnityEngine;
namespace ShaderMotion {
public class MeshUtil {
	public static KeyValuePair<int, Matrix4x4>[,] MergeBoneWeightBindposes(BoneWeight[] boneWeights, Matrix4x4[] bindposes, Transform[] bones, Transform[] armature, int quality=4, float threshold=0, int rootBone=0) {
		var rebinds = new KeyValuePair<int, Matrix4x4>[bones.Length];
		for(int i=0; i<bones.Length; i++) {
			var b = bones[i];
			while(b != null && Array.IndexOf(armature, b) < 0)
				b = b.parent;
			if(b == null && i != rootBone)
				Debug.LogWarning($"bone[\"{bones[i].name}\"] isn't a descendant of {armature[rootBone].name}");
			var idx = Array.IndexOf(armature, b);
			if(idx<0)
				idx = rootBone;
			rebinds[i] = new KeyValuePair<int, Matrix4x4>(idx,
				(armature[idx] ? armature[idx].worldToLocalMatrix : Matrix4x4.identity) *
				(bones[i] ? bones[i].localToWorldMatrix : Matrix4x4.identity) * bindposes[i]);
		}

		var boneWeightBindposes = new KeyValuePair<int, Matrix4x4>[boneWeights.Length, quality];
		for(int v=0; v<boneWeights.Length; v++) {
			var wmat = new Matrix4x4[armature.Length];
			var bws = new KeyValuePair<int, float>[4]{
				new KeyValuePair<int, float>(boneWeights[v].boneIndex0, boneWeights[v].weight0),
				new KeyValuePair<int, float>(boneWeights[v].boneIndex1, boneWeights[v].weight1),
				new KeyValuePair<int, float>(boneWeights[v].boneIndex2, boneWeights[v].weight2),
				new KeyValuePair<int, float>(boneWeights[v].boneIndex3, boneWeights[v].weight3)};
			foreach(var bw in bws)
				if(bw.Value > threshold) {
					var bmat = rebinds[bw.Key];
					for(int k=0; k<16; k++)
						wmat[bmat.Key][k] += bmat.Value[k]*bw.Value;
				}

			var sorted = wmat.Select((m, i) => new KeyValuePair<float, int>(-m[3,3], i))
							.OrderBy(p => p.Key).Select(p => p.Value).ToArray();
			var wsum = sorted.Take(quality).Sum(i => wmat[i][3,3]);
			if(Mathf.Abs(wsum-1) > 1e-5f)
				Debug.LogWarning($@"vertex is skinned with >{quality} bones {{{string.Join(", ",
					sorted.TakeWhile(i => wmat[i][3,3]>1e-5).Select(i=>$"{armature[i].name}").ToArray())}}}: truncated");

			for(int i=0; i<quality; i++) {
				var idx = sorted[i];
				for(int k=0; k<16; k++)
					wmat[idx][k] /= wsum; // normalize weights
				if(wmat[idx][3,3] > 0)
					boneWeightBindposes[v, i] = new KeyValuePair<int, Matrix4x4>(idx, wmat[idx]);
			}
		}
		return boneWeightBindposes;
	}
}
}