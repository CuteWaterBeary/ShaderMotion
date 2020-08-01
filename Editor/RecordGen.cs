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
	public struct Axes {
		static MethodInfo GetPreRotation  = typeof(Avatar).GetMethod("GetPreRotation", BindingFlags.NonPublic | BindingFlags.Instance);
		static MethodInfo GetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.NonPublic | BindingFlags.Instance);
		static MethodInfo GetLimitSign    = typeof(Avatar).GetMethod("GetLimitSign", BindingFlags.NonPublic | BindingFlags.Instance);
		static Regex Constrained = new Regex(@"Toes|Eye|Proximal|Intermediate|Distal$");

		public Quaternion preQ, postQ; // bone.localRotation * postQ * sign == preQ * sign * muscleQ
		public float sign;
		public Vector3 min, max; // 0 = locked, NaN = non-human or affected by twist distribution
		public Axes(Transform bone) {
			sign  = 1;
			postQ = Quaternion.LookRotation(Vector3.right, Vector3.forward); // Unity's convention: Y-axis = twist
			preQ  = bone.localRotation * postQ;
			min   = Vector3.zero * float.NaN;
			max   = Vector3.zero * float.NaN;
		}
		public Axes(Avatar avatar, HumanBodyBones humanBone) {
			var sign3 = (Vector3)GetLimitSign.Invoke(avatar, new object[]{humanBone});
			var signQ = Quaternion.LookRotation(new Vector3(0, 0, sign3.x*sign3.y),
												new Vector3(0, sign3.x*sign3.z, 0));
			// bake non-uniform sign into uniform sign
			preQ  = (Quaternion)GetPreRotation.Invoke(avatar, new object[]{humanBone}) * signQ;
			postQ = (Quaternion)GetPostRotation.Invoke(avatar, new object[]{humanBone}) * signQ;
			sign  = sign3.x*sign3.y*sign3.z;
			// rotation min/max
			min = max = Vector3.zero * (Constrained.IsMatch(HumanTrait.BoneName[(int)humanBone]) ? 0 : float.NaN);
			for(int i=0; i<3; i++) {
				var muscle = HumanTrait.MuscleFromBone((int)humanBone, i);
				if(muscle >= 0) { // use global limits since most avatars keep default values
					min[i] = HumanTrait.GetMuscleDefaultMin(muscle);
					max[i] = HumanTrait.GetMuscleDefaultMax(muscle);
				}
			}
			// hips uses motionQ
			if(humanBone == HumanBodyBones.Hips)
				ClearPreQ();
		}
		public void ClearPreQ() {
			// use motionQ instead of muscleQ: bone.localRotation * postQ * sign == motionQ * preQ * sign
			postQ *= Quaternion.Inverse(preQ);
			preQ   = Quaternion.identity;
			sign   = 1;
		}
	}
	public struct BoneData {
		public Axes axes;
		public int parent;
		public int[] channels;
	}
	static BoneData[] LoadBoneData(Animator animator, HumanBodyBones[] humanBones, Transform[] bones) {
		for(int i=0; i<humanBones.Length; i++)
			if(!bones[i])
				bones[i] = animator.GetBoneTransform(humanBones[i]);

		var data = Enumerable.Repeat(new BoneData{parent=-1}, bones.Length).ToArray();
		// axes/parent for human bones
		for(int i=0; i<humanBones.Length; i++) {
			data[i].axes = new Axes(animator.avatar, humanBones[i]);
			for(var b = humanBones[i]; data[i].parent < 0 && b != HumanBodyBones.Hips; ) {
				b = (HumanBodyBones)HumanTrait.GetParentBone((int)b);
				var idx = Array.IndexOf(humanBones, b);
				data[i].parent = idx >= 0 && bones[idx] ? idx : -1;
			}
		}
		// axes/parent for non-human bones
		for(int i=humanBones.Length; i<bones.Length; i++) {
			data[i].axes = new Axes(bones[i]);
			for(var b = bones[i]; data[i].parent < 0 && b != null; ) {
				b = b.parent;
				data[i].parent = b ? Array.IndexOf(bones, b) : -1;
			}
		}
		// retarget axes.preQ for parent change (hips is already set relative to root)
		for(int i=0; i<bones.Length; i++)
			if(bones[i] && !(i < humanBones.Length && humanBones[i] == HumanBodyBones.Hips)) {
				data[i].axes.preQ = Quaternion.Inverse((data[i].parent < 0 ? animator.transform : bones[data[i].parent]).rotation)
							* bones[i].parent.rotation * data[i].axes.preQ;
				if(data[i].parent < 0)
					data[i].axes.ClearPreQ();
			}
		// channels from axes
		for(int i=0; i<bones.Length; i++) {
			var chan = new List<int>();
			if(bones[i] && data[i].parent < 0)
				chan.AddRange(Enumerable.Range(3, 9)); // chan[3:6] = position, chan[6:12] = rotation matrix
			else
				for(int j=0; j<3; j++)
					if(!(data[i].axes.min[j] == 0 && data[i].axes.max[j] == 0))
						chan.Add(j);
			data[i].channels = chan.ToArray();
		}
		return data;
	}
	public static float GetHumanScale(Animator animator) {
		var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
		var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(animator.avatar)) as ModelImporter;
		// animator.humanScale is supposed to be used, but T-pose hips height avoids floating feet
		if(importer) { 
			var description = importer.humanDescription;
			foreach(var sb in description.skeleton)
				if(sb.name == hips.name)
					return animator.transform.InverseTransformPoint(hips.parent.TransformPoint(sb.position)).y;
		}
		var boneToRoot = animator.transform.worldToLocalMatrix * hips.localToWorldMatrix;
		return animator.humanScale * boneToRoot.lossyScale.y;
	}
	public static void GenRecordMesh(Animator animator, HumanBodyBones[] humanBones, Mesh mesh, Transform[] bones) {
		var B = LoadBoneData(animator, humanBones, bones);
		var root = animator.transform;
		var humanScale = GetHumanScale(animator);

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
				smr.rootBone.localScale = new Vector3(1,1,1) * GetHumanScale(animator);

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