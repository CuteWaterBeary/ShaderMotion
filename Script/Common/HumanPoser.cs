using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShaderMotion {
public class HumanPoser {
	public static (Vector3, Quaternion) GetRootMotion(ref HumanPose pose, Animator animator) {
		var anim = animator.transform;
		// bodyPosition/bodyRotation represents the center of mass relative to animator parent, divided by the human scale
		// anim.localScale seems ignored
		var rootQ = Quaternion.Inverse(anim.localRotation) * pose.bodyRotation;
		var rootT = Quaternion.Inverse(anim.localRotation) * (pose.bodyPosition-anim.localPosition/animator.humanScale);
		// Unity <=2019 seems to ignore localScale on the open interval (hips, anim) as well
#if !UNITY_2020_1_OR_NEWER // TODO: tested on 2021 but not 2020
		var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
		rootT = anim.InverseTransformPoint(hips.parent.TransformPoint(
			InverseTransformPointTQ(rootT*animator.humanScale, hips.parent, anim)))/animator.humanScale;
#endif
		return (rootT, rootQ);
	}
	static Vector3 InverseTransformPointTQ(Vector3 pos, Transform begin, Transform end) {
		var chainT = Vector3.zero;
		var chainQ = Quaternion.identity;
		for(var x = begin; x != end; x = x.parent) {
			chainT = x.localRotation * chainT + x.localPosition;
			chainQ = x.localRotation * chainQ;
		}
		return Quaternion.Inverse(chainQ) * (pos - chainT);
	}
	public static void SetHipsPositionRotation(ref HumanPose pose, Vector3 hipsT, Quaternion hipsQ, float humanScale) {
		var spreadQ = Quaternion.identity;
		foreach(var (i, scale) in spreadMassQ)
			spreadQ *= HumanAxes.SwingTwist(new Vector3(
				pose.muscles[HumanTrait.MuscleFromBone(i, 0)]*scale[0],
				pose.muscles[HumanTrait.MuscleFromBone(i, 1)]*scale[1],
				pose.muscles[HumanTrait.MuscleFromBone(i, 2)]*scale[2]));
		var t = Quaternion.LookRotation(Vector3.right, Vector3.forward);
		pose.bodyPosition = hipsT / humanScale;
		pose.bodyRotation = hipsQ * (t * spreadQ * Quaternion.Inverse(t));
		// TODO: account for mass center
	}
	public static void SetBoneSwingTwists(ref HumanPose pose, Vector3[] swingTwists) {
		System.Array.Clear(pose.muscles, 0, pose.muscles.Length);
		for(int i=0; i<HumanTrait.BoneCount; i++)
			for(int j=0; j<3; j++) {
				var (muscle, weight) = boneMuscles[i, j];
				if(muscle >= 0)
					pose.muscles[muscle] += swingTwists[i][j] * weight;
			}
		for(int i=0; i<HumanTrait.MuscleCount; i++)
			pose.muscles[i] /= pose.muscles[i] >= 0 ? muscleLimits[i,1] : -muscleLimits[i,0];
	}
	public static readonly (int, Vector3)[] spreadMassQ = new[]{
		((int)HumanBodyBones.Spine,			new Vector3(20, -30, -30)),
		((int)HumanBodyBones.Chest,			new Vector3(20, -20, -20)),
		((int)HumanBodyBones.UpperChest,	new Vector3(10, -10, -10)),
	};
	public static readonly (int, float)[,] boneMuscles;
	public static readonly float[,] muscleLimits;
	static HumanPoser() {
		boneMuscles = new (int, float)[HumanTrait.BoneCount, 3];
		for(int i=0; i<HumanTrait.BoneCount; i++) 
			for(int j=0; j<3; j++) {
				var ii = i;
				var jj = j;
				var muscle = HumanTrait.MuscleFromBone(ii, jj);
				var weight = (float)1;
				if(muscle < 0) {
					switch(ii) {
					case (int)HumanBodyBones.LeftShoulder:
						ii = (int)HumanBodyBones.LeftUpperArm; break;
					case (int)HumanBodyBones.RightShoulder:
						ii = (int)HumanBodyBones.RightUpperArm; break;
					case (int)HumanBodyBones.Jaw:
						break;
					case (int)HumanBodyBones.LeftLowerArm:
					case (int)HumanBodyBones.RightLowerArm:
						weight = -1;
						jj = 0;
						goto default;
					case (int)HumanBodyBones.LeftLowerLeg:
					case (int)HumanBodyBones.RightLowerLeg:
						jj = 0;
						goto default;
					default:
						ii = HumanTrait.GetParentBone(ii);break;
					}
					muscle = HumanTrait.MuscleFromBone(ii, jj);
				}
				boneMuscles[i, j] = (muscle, weight);
			}
		muscleLimits = new float[HumanTrait.MuscleCount, 2];
		for(int i=0; i<HumanTrait.MuscleCount; i++) {
			muscleLimits[i, 0] = HumanTrait.GetMuscleDefaultMin(i);
			muscleLimits[i, 1] = HumanTrait.GetMuscleDefaultMax(i);
		}
	}
}
}