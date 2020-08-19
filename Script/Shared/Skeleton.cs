using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ShaderMotion {
public class Skeleton {
	public readonly Transform[] bones;
	public readonly BoneAxes[] axes;
	public readonly int[] parents;
	public readonly Transform root;
	public readonly float scale;
	public Skeleton(Animator animator, Transform[] genericBones=null) {
		scale = GetSkeletonPoseHipsHeight(animator); // animator.humanScale causes floating feet
		root  = animator.transform;
		bones = Enumerable.Range(0, HumanTrait.BoneCount)
			.Select(i => animator.GetBoneTransform((HumanBodyBones)i))
			.Concat(genericBones ?? Enumerable.Empty<Transform>()).ToArray();
		// set up axes/parents
		axes = new BoneAxes[bones.Length];
		parents = Enumerable.Repeat(-1, bones.Length).ToArray();
		for(int i=0; i<HumanTrait.BoneCount; i++) { // human bones
			axes[i] = new BoneAxes(animator.avatar, (HumanBodyBones)i);
			for(var b = i; parents[i] < 0 && b != (int)HumanBodyBones.Hips; ) {
				b = HumanTrait.GetParentBone(b);
				parents[i] = b >= 0 && bones[b] ? b : -1;
			}
		}
		for(int i=HumanTrait.BoneCount; i<bones.Length; i++) { // generic bones
			axes[i] = new BoneAxes(bones[i]);
			for(var b = bones[i]; parents[i] < 0 && b != null; ) {
				b = b.parent;
				parents[i] = b ? Array.IndexOf(bones, b) : -1;
			}
		}
		// retarget axes.preQ for parent change (hips is already set relative to root)
		for(int i=0; i<bones.Length; i++)
			if(bones[i] && i != (int)HumanBodyBones.Hips) {
				axes[i].preQ = Quaternion.Inverse((parents[i] < 0 ? root : bones[parents[i]]).rotation)
							* bones[i].parent.rotation * axes[i].preQ;
				if(parents[i] < 0)
					axes[i].ClearPreQ();
			}
	}
	public static float GetSkeletonPoseHipsHeight(Animator animator) {
		Vector3 pos;
		if(GetSkeletonPosePosition(animator.avatar, 0, out pos))
			return pos.y;
		// guess from HumanPose if SkeletonPose is not available
		var humanPose = new HumanPose();
		new HumanPoseHandler(animator.avatar, animator.transform).GetHumanPose(ref humanPose);
		var root = animator.transform;
		var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
		var offset = Vector3.Scale(hips.parent.lossyScale, humanPose.bodyPosition-root.position/animator.humanScale);
		return (hips.position-root.position).magnitude / offset.magnitude;
	}
	static bool GetSkeletonPosePosition(Avatar avatar, int axesId, out Vector3 pos) {
		pos = default(Vector3);
		#if UNITY_EDITOR
			var human = new SerializedObject(avatar).FindProperty("m_Avatar.m_Human.data");
			var skeletonNode = human.FindPropertyRelative("m_Skeleton.data.m_Node");
			var skeletonPose = human.FindPropertyRelative("m_SkeletonPose.data.m_X");
			for(int i=0; i<skeletonNode.arraySize; i++)
				if(axesId == skeletonNode.GetArrayElementAtIndex(i).FindPropertyRelative("m_AxesId").intValue) {
					var pose = skeletonPose.GetArrayElementAtIndex(i);
					pos = new Vector3(	pose.FindPropertyRelative("t.x").floatValue,
										pose.FindPropertyRelative("t.y").floatValue,
										pose.FindPropertyRelative("t.z").floatValue);
					return true;
				}
		#endif
		return false;
	}
}
}