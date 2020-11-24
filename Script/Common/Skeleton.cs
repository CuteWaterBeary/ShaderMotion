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
	public readonly HumanAxes[] axes;
	public readonly bool[] dummy;
	public readonly int[] parents;
	public readonly Transform root;
	public readonly float humanScale;
	public Skeleton(Animator animator, Transform[] genericBones=null) {
		humanScale = GetSkeletonPoseHipsHeight(animator); // animator.humanScale causes floating feet
		root  = animator.transform;
		bones = Enumerable.Range(0, HumanTrait.BoneCount)
			.Select(i => animator.GetBoneTransform((HumanBodyBones)i))
			.Concat(genericBones ?? Enumerable.Empty<Transform>()).ToArray();
		axes  = Enumerable.Range(0, HumanTrait.BoneCount)
			.Select(i => new HumanAxes(animator.avatar, (HumanBodyBones)i))
			.Concat((genericBones ?? Enumerable.Empty<Transform>()).Select(t => new HumanAxes(t))).ToArray();
		// add dummy human bones so that animation on missing UpperChest is handled correctly
		dummy = new bool[bones.Length];
		foreach(var (i, p) in dummyHumanBones)
			if(!bones[i] && bones[p])
				(bones[i], axes[i], dummy[i]) = (bones[p], axes[p], true);
		// bone hierarchy
		parents = Enumerable.Repeat(-1, bones.Length).ToArray();
		for(int i=0; i<HumanTrait.BoneCount; i++) // human bones use human hierarchy
			for(var b = i; parents[i] < 0 && b != (int)HumanBodyBones.Hips; ) {
				b = HumanTrait.GetParentBone(b);
				parents[i] = b >= 0 && bones[b] ? b : -1;
			}
		for(int i=HumanTrait.BoneCount; i<bones.Length; i++) // generic bones use transform hierarchy
			for(var b = bones[i]; parents[i] < 0 && b != null; ) {
				b = b.parent;
				parents[i] = b ? Array.LastIndexOf(bones, b) : -1;
			}
		// fix constraints of root bones like Hips: (bone.rotation * postQ == rotationMatrix * preQ)
		for(int i=0; i<bones.Length; i++)
			if(bones[i] && parents[i] < 0) {
				axes[i].postQ *= Quaternion.Inverse(axes[i].preQ);
				axes[i].preQ = Quaternion.Inverse(bones[i].parent.rotation) * root.rotation;
				axes[i].sign = 1;
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
	private static bool GetSkeletonPosePosition(Avatar avatar, int axesId, out Vector3 pos) {
		pos = default(Vector3);
		#if UNITY_EDITOR // this can be improved when avatar.humanDescription is exposed in unity 2019
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
	private static readonly (int,int)[] dummyHumanBones = new (int,int)[]{
		((int)HumanBodyBones.Chest, 		(int)HumanBodyBones.Spine),
		((int)HumanBodyBones.UpperChest,	(int)HumanBodyBones.Chest),
		((int)HumanBodyBones.Neck,			(int)HumanBodyBones.Head),
	};
}
}