#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;

namespace ShaderMotion {
public partial class MeshGen {
	public static void GenRecordMesh(Animator animator, HumanBodyBones[] humanBones, Mesh mesh, Transform[] bones) {
		var B = HumanUtil.LoadBoneData(animator, humanBones, bones);
		var root = animator.transform;
		var humanScale = HumanUtil.GetHumanScale(animator);

		var bindposes = bones.Select(b => Matrix4x4.Scale( // unscale bone in bindpose
							(root.worldToLocalMatrix * (b??root).localToWorldMatrix).lossyScale).inverse).ToArray();
		var bounds = new Bounds();
		var vertices = new List<Vector3>(Enumerable.Repeat(Vector3.zero, 3)); // background quad
		var normals  = new List<Vector3>(Enumerable.Repeat(Vector3.zero, 3));
		var tangents = new List<Vector4>(Enumerable.Repeat(new Vector4(0,0,0, -1), 3));
		var boneWeights = new List<BoneWeight>(Enumerable.Repeat(new BoneWeight(), 3));
		var nchan = 0;
		for(int i=0; i<bones.Length; i++) {
			if(bones[i])
				bounds.Encapsulate(root.InverseTransformPoint(bones[i].position));
			var par   = bones[i] ? B[i].parent : i;
			var preQ  = bones[i] ? B[i].axes.preQ  : Quaternion.identity;
			var postQ = bones[i] ? B[i].axes.postQ : Quaternion.identity;
			var preM  = Matrix4x4.Rotate(preQ);
			var postM = Matrix4x4.Rotate(postQ);
			foreach(var j in B[i].channels) {
				vertices.Add(Vector3.zero);
				normals. Add(humanScale * preM.GetColumn(1));
				tangents.Add(humanScale * preM.GetColumn(2) + new Vector4(0,0,0, nchan));
				boneWeights.Add(par >= 0 ? new BoneWeight{boneIndex0=par, weight0=1/humanScale} : new BoneWeight{});

				vertices.Add(Vector3.zero);
				normals. Add(humanScale * postM.GetColumn(1));
				tangents.Add(humanScale * postM.GetColumn(2) + new Vector4(0,0,0, B[i].axes.sign * (j+1)));
				boneWeights.Add(new BoneWeight{boneIndex0=i, weight0=1/humanScale});

				vertices.Add(Vector3.zero);
				normals. Add(Vector3.zero);
				tangents.Add(Vector4.zero);
				boneWeights.Add(new BoneWeight{boneIndex0=i, weight0=1/humanScale});
				nchan++;
			}
		}
		Debug.Log($"#channels={nchan}");

		mesh.Clear();
		mesh.ClearBlendShapes();
		mesh.subMeshCount = 1;
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
		mesh.SetVertices(vertices);
		mesh.SetNormals (normals );
		mesh.SetTangents(tangents);
		mesh.boneWeights = boneWeights.ToArray();
		mesh.bindposes = bindposes;
		mesh.triangles = Enumerable.Range(0, vertices.Count).ToArray();
		mesh.bounds = bounds;
	}
	[MenuItem("ShaderMotion/Generate Recorder")]
	static void GenRecordMesh() {
		var animator = Selection.activeGameObject.GetComponentInParent<Animator>();
		if(!(animator && animator.isHuman)) {
			Debug.LogError($"Expect a human Animator on {Selection.activeGameObject}");
			return;
		}
		var path0 = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)),
							animator.name);

		var smr = animator.transform.Find("MotionRecord")?.GetComponent<SkinnedMeshRenderer>();
		if(!smr) {
			var mesh = new Mesh();
			var path = $"{path0}_record.asset";
			AssetDatabase.CreateAsset(mesh, path);
			Debug.Log($"Create mesh @ {path}");

			var go = new GameObject("MotionRecord", typeof(SkinnedMeshRenderer));
			go.transform.SetParent(animator.transform, false);
			smr = go.GetComponent<SkinnedMeshRenderer>();
			smr.sharedMesh = mesh;
		}
		var mat = smr.sharedMaterial;
		if(!mat)
			smr.sharedMaterial = mat = Resources.Load<Material>("MotionRecord");

		{
			var mesh = smr.sharedMesh;
			var bones = new Transform[humanBodyBones.Length];
			GenRecordMesh(animator, humanBodyBones, mesh, bones);
			smr.bones = bones;
			AssetDatabase.SaveAssets();

			if(!smr.rootBone)
				smr.rootBone = animator.transform;
			// rescale the anchor, useful for boundary safety system
			if(smr.rootBone != animator.transform)
				smr.rootBone.localScale = new Vector3(1,1,1) * HumanUtil.GetHumanScale(animator);

			var scale = smr.rootBone.localScale;
			scale = new Vector3(1/scale.x, 1/scale.y, 1/scale.z);
			smr.localBounds = new Bounds(Vector3.Scale(scale,mesh.bounds.center),
										Vector3.Scale(scale,mesh.bounds.size));
		}
	}

	public static HumanBodyBones[] humanBodyBones = new []{
		// mostly ordered by BoneDefaultHierarchyMass, except arms are emphasized over legs
		// column 0
		HumanBodyBones.Hips,
		HumanBodyBones.Jaw, // sync
		HumanBodyBones.Spine,
		HumanBodyBones.Chest,
		HumanBodyBones.UpperChest, // sync
		HumanBodyBones.Neck,
		HumanBodyBones.Head,
		HumanBodyBones.LeftShoulder,
		HumanBodyBones.RightShoulder,
		HumanBodyBones.LeftUpperArm,
		HumanBodyBones.RightUpperArm,
		HumanBodyBones.LeftLowerArm,
		HumanBodyBones.RightLowerArm,
		// column 1
		HumanBodyBones.LeftUpperLeg,
		HumanBodyBones.RightUpperLeg,
		HumanBodyBones.LeftLowerLeg,
		HumanBodyBones.RightLowerLeg,
		HumanBodyBones.LeftFoot,
		HumanBodyBones.RightFoot,
		HumanBodyBones.LeftToes,  // sync
		HumanBodyBones.RightToes, // sync
		HumanBodyBones.LeftHand,
		HumanBodyBones.RightHand,
		HumanBodyBones.LeftEye,
		HumanBodyBones.RightEye,
		// fingers
		HumanBodyBones.LeftThumbProximal,
		HumanBodyBones.LeftThumbIntermediate,
		HumanBodyBones.LeftThumbDistal,
		HumanBodyBones.LeftIndexProximal,
		HumanBodyBones.LeftIndexIntermediate,
		HumanBodyBones.LeftIndexDistal,
		HumanBodyBones.LeftMiddleProximal,
		HumanBodyBones.LeftMiddleIntermediate,
		HumanBodyBones.LeftMiddleDistal,
		HumanBodyBones.LeftRingProximal,
		HumanBodyBones.LeftRingIntermediate,
		HumanBodyBones.LeftRingDistal,
		HumanBodyBones.LeftLittleProximal,
		HumanBodyBones.LeftLittleIntermediate,
		HumanBodyBones.LeftLittleDistal,
		HumanBodyBones.RightThumbProximal,
		HumanBodyBones.RightThumbIntermediate,
		HumanBodyBones.RightThumbDistal,
		HumanBodyBones.RightIndexProximal,
		HumanBodyBones.RightIndexIntermediate,
		HumanBodyBones.RightIndexDistal,
		HumanBodyBones.RightMiddleProximal,
		HumanBodyBones.RightMiddleIntermediate,
		HumanBodyBones.RightMiddleDistal,
		HumanBodyBones.RightRingProximal,
		HumanBodyBones.RightRingIntermediate,
		HumanBodyBones.RightRingDistal,
		HumanBodyBones.RightLittleProximal,
		HumanBodyBones.RightLittleIntermediate,
		HumanBodyBones.RightLittleDistal,
	};
}
}
#endif