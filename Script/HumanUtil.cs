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
public class HumanUtil {
	public struct Axes {
		static MethodInfo GetPreRotation  = typeof(Avatar).GetMethod("GetPreRotation", BindingFlags.NonPublic | BindingFlags.Instance);
		static MethodInfo GetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.NonPublic | BindingFlags.Instance);
		static MethodInfo GetLimitSign    = typeof(Avatar).GetMethod("GetLimitSign", BindingFlags.NonPublic | BindingFlags.Instance);
		static Regex Constrained = new Regex(@"Jaw|Toes|Eye|Proximal|Intermediate|Distal$");

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
	public class Armature {
		public HumanBodyBones[] humanBones;
		public Transform[] bones;
		public Axes[] axes;
		public int[] parents;
		public Transform root;
		public float scale;
		public Armature(Animator animator, HumanBodyBones[] humanBones, Transform[] bones=null) {
			root = animator.transform;
			scale = GetHumanScale(animator);
			// set up bones
			this.humanBones = humanBones;
			this.bones = bones = bones ?? new Transform[humanBones.Length];
			for(int i=0; i<humanBones.Length; i++)
				if(!bones[i])
					bones[i] = animator.GetBoneTransform(humanBones[i]);
			// set up axes/parents
			axes = new Axes[bones.Length];
			parents = Enumerable.Repeat(-1, bones.Length).ToArray();
			for(int i=0; i<humanBones.Length; i++) { // human bones
				axes[i] = new Axes(animator.avatar, humanBones[i]);
				for(var b = humanBones[i]; parents[i] < 0 && b != HumanBodyBones.Hips; ) {
					b = (HumanBodyBones)HumanTrait.GetParentBone((int)b);
					var idx = Array.IndexOf(humanBones, b);
					parents[i] = idx >= 0 && bones[idx] ? idx : -1;
				}
			}
			for(int i=humanBones.Length; i<bones.Length; i++) { // non-human bones
				axes[i] = new Axes(bones[i]);
				for(var b = bones[i]; parents[i] < 0 && b != null; ) {
					b = b.parent;
					parents[i] = b ? Array.IndexOf(bones, b) : -1;
				}
			}
			// retarget axes.preQ for parent change (hips is already set relative to root)
			for(int i=0; i<bones.Length; i++)
				if(bones[i] && !(i < humanBones.Length && humanBones[i] == HumanBodyBones.Hips)) {
					axes[i].preQ = Quaternion.Inverse((parents[i] < 0 ? animator.transform : bones[parents[i]]).rotation)
								* bones[i].parent.rotation * axes[i].preQ;
					if(parents[i] < 0)
						axes[i].ClearPreQ();
				}
		}
	}
}
}