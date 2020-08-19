using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using UnityEngine;
namespace ShaderMotion {
public class MeshUtil {
	public static (int, Matrix4x4)[] RetargetBindposes(Matrix4x4[] bindposes, Transform[] bones, Transform[] targetBones, int rootBone) {
		var targetBindposes = new (int, Matrix4x4)[bones.Length];
		for(int i=0; i<bones.Length; i++) {
			var j = -1;
			for(var b = bones[i]; b != null && j < 0; b = b.parent)
				j = Array.IndexOf(targetBones, b);
			if(j < 0)
				j = rootBone;
			targetBindposes[i] = (j, (targetBones[j] ? targetBones[j].worldToLocalMatrix : Matrix4x4.identity) *
								(bones[i] ? bones[i].localToWorldMatrix : Matrix4x4.identity) * bindposes[i]);
		}
		return targetBindposes;
	}
	public static (int, Matrix4x4)[,] RetargetWeightBindposes(BoneWeight[] boneWeights, Matrix4x4[] bindposes, Transform[] bones, Transform[] targetBones, int rootBone, int quality=4) {
		var targetBindposes = RetargetBindposes(bindposes, bones, targetBones, rootBone);
		var targetWeightBindposes = new (int, Matrix4x4)[boneWeights.Length, quality];
		for(int v=0; v<boneWeights.Length; v++) {
			var wbs = new Matrix4x4[targetBones.Length];
			var bws = new (int, float)[4]{
				(boneWeights[v].boneIndex0, boneWeights[v].weight0),
				(boneWeights[v].boneIndex1, boneWeights[v].weight1),
				(boneWeights[v].boneIndex2, boneWeights[v].weight2),
				(boneWeights[v].boneIndex3, boneWeights[v].weight3)};
			foreach(var bw in bws)
				for(int k=0; k<16; k++)
					wbs[targetBindposes[bw.Item1].Item1][k] += targetBindposes[bw.Item1].Item2[k]*bw.Item2;

			var sorted = Enumerable.Range(0, wbs.Length).OrderBy(i => -wbs[i][3,3]).ToArray();
			var ratio  = sorted.Take(quality).Sum(i => wbs[i][3,3]) / sorted.Sum(i => wbs[i][3,3]);
			if(Mathf.Abs(ratio-1) > 1e-5f)
				Debug.LogWarning($@"vertex is skinned with >{quality} bones: skip {string.Join(", ",
					sorted.Skip(quality).TakeWhile(i => wbs[i][3,3]>1e-5).Select(i=>targetBones[i].name))}");

			for(int i=0; i<quality; i++) {
				var b = sorted[i];
				for(int k=0; k<16; k++)
					wbs[b][k] /= ratio; // normalize weights
				if(wbs[b][3,3] > 0)
					targetWeightBindposes[v, i] = (b, wbs[b]);
			}
		}
		return targetWeightBindposes;
	}
	public static void FixNormalTangents(Mesh mesh, ref Vector3[] normals, ref Vector4[] tangents) {
		if(normals.Length < mesh.vertexCount || tangents.Length < mesh.vertexCount) {
			mesh = Object.Instantiate(mesh);
			if(normals.Length < mesh.vertexCount)
				mesh.RecalculateNormals();
			if(tangents.Length < mesh.vertexCount)
				mesh.RecalculateTangents();
			normals  = mesh.normals;
			tangents = mesh.tangents;
			Object.DestroyImmediate(mesh);
		}
	}
}
}