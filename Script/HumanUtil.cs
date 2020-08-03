using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Reflection;
using System.Text.RegularExpressions;

namespace ShaderMotion {
public partial class HumanUtil {
	public struct Axes {
		static MethodInfo GetPreRotation  = typeof(Avatar).GetMethod("GetPreRotation", BindingFlags.NonPublic | BindingFlags.Instance);
		static MethodInfo GetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.NonPublic | BindingFlags.Instance);
		static MethodInfo GetLimitSign    = typeof(Avatar).GetMethod("GetLimitSign", BindingFlags.NonPublic | BindingFlags.Instance);
		static Regex Constrained = new Regex(@"Toes|Eye|Proximal|Intermediate|Distal$");

		public Quaternion preQ, postQ; // bone.localRotation * postQ == preQ * muscleQ(sign * angle)
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
			// bake non-uniform sign into uniform sign:
			// muscleQ(sign3 * angle) == det(sign3)flip(sign3) * muscleQ(det(sign3) * angle) * det(sign3)flip(sign3)
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
			// use motionQ instead of muscleQ: bone.localRotation * postQ == motionQ * preQ
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
	public static BoneData[] LoadBoneData(Animator animator, HumanBodyBones[] humanBones, Transform[] bones) {
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
		HumanDescription? humanDescription = null;
		#if UNITY_EDITOR
			humanDescription = (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(animator.avatar))
										as ModelImporter)?.humanDescription;
		#endif
		// animator.humanScale is supposed to be used, but T-pose hips height avoids floating feet
		if(humanDescription != null)
			foreach(var sb in humanDescription?.skeleton)
				if(sb.name == hips.name)
					return animator.transform.InverseTransformPoint(hips.parent.TransformPoint(sb.position)).y;
		// otherwise, determine root scale from HumanPose
		var pose = new HumanPose();
		new HumanPoseHandler(animator.avatar, animator.transform).GetHumanPose(ref pose);
		return animator.transform.InverseTransformPoint(hips.position).y/pose.bodyPosition.y;
	}
}
}