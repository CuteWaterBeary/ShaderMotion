using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ShaderMotion {
public class HumanPoser {
	public static (Vector3, Quaternion) GetRootMotion(ref HumanPose pose, Animator animator) {
		// unfortunately we can't simply read bodyPosition & bodyRotation
		// because Unity ignores localScale for ancestors of hips
		var root = animator.transform;
		var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
		var rootQ = root.localRotation;
		var rootT = root.localPosition/animator.humanScale;
		// undo animator's localTQ
		var massQ = Quaternion.Inverse(rootQ) * pose.bodyRotation;
		var massT = Quaternion.Inverse(rootQ) * (pose.bodyPosition - rootT);
 		// TR(bodyPosition, bodyRotation) == p[0].localTR  * .. * p[n].localTR  * hips.localTR
 		// TRS(massT, massQ, hipsS)       == p[0].localTRS * .. * p[n].localTRS * hips.localTR
		var preT = Vector3.zero;
		for(var x = hips.parent; x != root; x = x.parent)
			preT = x.localPosition + x.localRotation * preT;
		massT = root.InverseTransformPoint(hips.parent.TransformPoint(
			Quaternion.Inverse(hips.parent.rotation) * (root.rotation * (
				massT * animator.humanScale - preT))));
		return (massT / animator.humanScale, massQ);
	}
	public static void SetHipsPositionRotation(ref HumanPose pose, Vector3 hipsT, Quaternion hipsQ) {
		var spreadQ = Quaternion.identity;
		foreach(var (i, scale) in spreadMassQ)
			spreadQ *= BoneAxes.SwingTwist(new Vector3(
				pose.muscles[HumanTrait.MuscleFromBone(i, 0)]*scale[0],
				pose.muscles[HumanTrait.MuscleFromBone(i, 1)]*scale[1],
				pose.muscles[HumanTrait.MuscleFromBone(i, 2)]*scale[2]));
		var t = Quaternion.LookRotation(Vector3.right, Vector3.forward);
		pose.bodyPosition = hipsT;
		pose.bodyRotation = hipsQ * (t * spreadQ * Quaternion.Inverse(t));
	}
	private static readonly (int, Vector3)[] spreadMassQ = new[]{
		((int)HumanBodyBones.Spine,			new Vector3(20, -30, -30)),
		((int)HumanBodyBones.Chest,			new Vector3(20, -20, -20)),
		((int)HumanBodyBones.UpperChest,	new Vector3(10, -10, -10)),
	};
}
}