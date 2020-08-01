using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ShaderMotion {
public partial class MeshGen {
	public static void GenReplayMesh(Animator animator, HumanBodyBones[] humanBones, Mesh mesh, Transform[] bones, Mesh srcMesh, Transform[] srcBones, Texture2D tex, int quality=2) {
		var B = LoadBoneData(animator, humanBones, bones);
		var root = animator.transform;
		var humanScale = GetHumanScale(animator);
		var hipsIndex = Array.IndexOf(humanBones, HumanBodyBones.Hips);

		// generate armature LUT
		{
			var extraData = new int[bones.Length, 2];
			var nchan = 0;
			for(int i=0; i<bones.Length; i++) {
				var start = nchan - (B[i].channels[0] - (B[i].channels[0] < 3 ? 0 : 3));
				var mask = B[i].channels.Sum(j => 1<<j);
				nchan += B[i].channels.Length;
				extraData[i, 0] = start;
				extraData[i, 1] = (mask & 7) + (B[i].axes.sign > 0 ? 0 : 8);
			}
			Debug.Log($"#channels={nchan}");

			tex.Resize(bones.Length*2, bones.Length, TextureFormat.RGBAFloat, false);
			var colors = Enumerable.Repeat(new Color(0,0,0,-1), tex.width * tex.height).ToArray();
			for(int i=0; i<bones.Length; i++) if(bones[i]) {
				var ancestors = new List<int>();
				for(int j = i; j >= 0; j = B[j].parent)
					ancestors.Add(j);
				ancestors.Reverse();
				for(int j=0; j<ancestors.Count; j++) {
					var b = ancestors[j];
					var p = B[b].parent;
					var bone = bones[b];
					var par = p < 0 ? root : bones[p];
					var invQ = Quaternion.Inverse(p < 0 ? par.rotation : par.rotation * B[p].axes.postQ);
					var pos = (invQ * root.rotation) * root.InverseTransformVector(bone.position - par.position);
					var rot = (invQ * par.rotation * B[b].axes.preQ).eulerAngles * Mathf.Deg2Rad;
					if(p < 0) // save scale instead of position for motionT
						pos = new Vector3(1,1,1) * humanScale;
					colors[(i)*tex.width + j*2 + 0] = new Color(pos.x, pos.y, pos.z, extraData[b, 0]);
					colors[(i)*tex.width + j*2 + 1] = new Color(rot.x, rot.y, rot.z, extraData[b, 1]);
				}
			}
			tex.SetPixels(colors);
			tex.Apply(false, false);
		}

		// rescale bone in bindpose because motion armature has no scale
		var bindposes = bones.Select(b => Matrix4x4.Scale(
							(root.worldToLocalMatrix * (b??root).localToWorldMatrix).lossyScale)).ToArray();
		var bwBinds = MeshUtil.MergeBoneWeightBindposes(srcMesh.boneWeights, srcMesh.bindposes, srcBones, bones, quality:quality, rootBone:hipsIndex);
		for(int v=0; v<srcMesh.vertexCount; v++)
			for(int i=0; i<quality; i++) {
				var bone = bwBinds[v, i].Key;
				var bind = bwBinds[v, i].Value;
				if(bind[3,3] != 0)
					bwBinds[v, i] = new KeyValuePair<int, Matrix4x4>(bone,
										Matrix4x4.Rotate(Quaternion.Inverse(B[bone].axes.postQ)) * bindposes[bone] * bind);
			}

		var srcVertices = srcMesh.vertices;
		var srcNormals  = srcMesh.normals;
		var srcUVs      = srcMesh.uv;
		var uvSkin = Array.ConvertAll(new int[quality], x => new[]{new List<Vector4>(), new List<Vector4>()});
		for(int v=0; v<srcMesh.vertexCount; v++)
			for(int i=0; i<quality; i++) {
				var bone = bwBinds[v, i].Key;
				var bind = bwBinds[v, i].Value;
				var weight = bind[3,3];
				var vertex = bind.MultiplyPoint3x4(srcVertices[v]);
				var normal = bind.MultiplyVector  (srcNormals[v]);

				uvSkin[i][0].Add(new Vector4(vertex.x, vertex.y, vertex.z, weight/2 + bone));
				uvSkin[i][1].Add(new Vector4(normal.x, normal.y, normal.z, srcUVs[v][i]));
			}
		mesh.Clear();
		mesh.ClearBlendShapes();
		mesh.subMeshCount = 1;
		mesh.indexFormat = srcMesh.vertexCount < 65536 ? UnityEngine.Rendering.IndexFormat.UInt16
													: UnityEngine.Rendering.IndexFormat.UInt32;


		var objectToRoot = root.worldToLocalMatrix * bones[hipsIndex].localToWorldMatrix
							* srcMesh.bindposes[Array.IndexOf(srcBones, bones[hipsIndex])];
		mesh.vertices = Array.ConvertAll(srcMesh.vertices, v => objectToRoot.MultiplyPoint3x4(v)); // rest pose
		mesh.triangles = srcMesh.triangles;
		for(int i=0; i<quality; i++)
			for(int j=0; j<2; j++)
				mesh.SetUVs(i*2+j, uvSkin[i][j]);

		var bounds = mesh.bounds;
		bounds.min -= new Vector3(1,0,1) * humanScale;
		bounds.max += new Vector3(1,0,1) * humanScale;
		mesh.bounds = bounds;
	}
	[MenuItem("ShaderMotion/Generate Replayer")]
	static void GenReplayMesh() {
		var smr = Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>();
		if(!smr) {
			Debug.LogError($"Require a SkinnedMeshRenderer on {Selection.activeGameObject}");
			return;
		}
		var animator = smr.gameObject.GetComponentInParent<Animator>();
		if(!(animator && animator.isHuman)) {
			Debug.LogError($"Expect a human Animator");
			return;
		}
		var path0 = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)),
							animator.name);

		var mr = smr.transform.parent.Find("MotionReplay")?.GetComponent<MeshRenderer>();
		if(!mr) {
			var mesh = new Mesh();
			var path = $"{path0}_replay.asset";
			AssetDatabase.CreateAsset(mesh, path);
			Debug.Log($"Create mesh @ {path}");

			var go = new GameObject("MotionReplay", typeof(MeshRenderer), typeof(MeshFilter));
			go.transform.SetParent(animator.transform, false);
			mr = go.GetComponent<MeshRenderer>();
			mr.GetComponent<MeshFilter>().sharedMesh = mesh;
		}
		var mat = mr.sharedMaterial;
		if(!mat) {
			mr.sharedMaterial = mat = Object.Instantiate(Resources.Load<Material>("MotionReplay"));
			var path = $"{path0}_replay.mat";
			AssetDatabase.CreateAsset(mat, path);
			Debug.Log($"Create material @ {path}");

			mat.mainTexture = smr.sharedMaterial.mainTexture;
			mat.color = smr.sharedMaterial.color;
		}
		var tex = (Texture2D)mat.GetTexture("_Armature");
		if(!tex) {
			tex = new Texture2D(1,1);
			var path = $"{path0}_armature.asset";
			AssetDatabase.CreateAsset(tex, path);
			Debug.Log($"Create texture @ {path}");

			mat.SetTexture("_Armature", tex);
		}

		var dstMesh = mr.GetComponent<MeshFilter>().sharedMesh;
		var dstBones = new Transform[humanBodyBones.Length];
		GenReplayMesh(animator, humanBodyBones, dstMesh, dstBones, smr.sharedMesh, smr.bones, tex);

		AssetDatabase.SaveAssets();
	}
}
}