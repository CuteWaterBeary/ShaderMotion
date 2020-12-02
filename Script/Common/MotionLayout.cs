using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;

namespace ShaderMotion {
public class MotionLayout {
	public readonly int[][] bones;
	public readonly int[] exprs;
	public MotionLayout(Skeleton skeleton, (int, int, int)[] boneLayout, Appearance appr, (int,int)[] exprLayout) {
		bones = new int[skeleton.bones.Length][];
		int index = 0;
		foreach(var (startIndex, length, bone) in boneLayout) {
			index = startIndex >= 0 ? startIndex : index;
			var endIndex = index + length;
			if(length <= 3) {
				bones[bone] = Enumerable.Repeat(-1, 3).ToArray();
				for(int i=0; i<3; i++)
					if(length == 3 || HumanTrait.MuscleFromBone(bone, i) >= 0)
						bones[bone][i] = index++;
			} else if(length == 12) {
				bones[bone] = Enumerable.Repeat(-1, 3).Concat(Enumerable.Range(index, 12)).ToArray();
				index += 12;
			}
			Debug.Assert(index == endIndex);
		}
		exprs = Enumerable.Range(-1, appr.exprShapes.Length).ToArray();
		foreach(var (startIndex, expr) in exprLayout)
			exprs[expr] = startIndex;
	}

	public static (int, int)[] defaultExprLayout = new []{
		(80, 0),
		(81, 1),
		(82, 2),
		(83, 3),
		(84, 4),
	};
	public static (int, int, int)[] defaultHumanLayout = new []{
		// roughly ordered by HumanTrait.GetBoneDefaultHierarchyMass

		(  0,12, (int)HumanBodyBones.Hips),
		( -1, 3, (int)HumanBodyBones.Spine),
		( -1, 3, (int)HumanBodyBones.Chest),
		( -1, 3, (int)HumanBodyBones.UpperChest),
		( -1, 3, (int)HumanBodyBones.Neck),
		( -1, 3, (int)HumanBodyBones.Head),

		( 27, 3, (int)HumanBodyBones.LeftUpperLeg),
		( -1, 3, (int)HumanBodyBones.RightUpperLeg),
		( -1, 3, (int)HumanBodyBones.LeftLowerLeg),
		( -1, 3, (int)HumanBodyBones.RightLowerLeg),
		( -1, 3, (int)HumanBodyBones.LeftFoot),
		( -1, 3, (int)HumanBodyBones.RightFoot),

		( 45, 3, (int)HumanBodyBones.LeftShoulder),
		( -1, 3, (int)HumanBodyBones.RightShoulder),
		( -1, 3, (int)HumanBodyBones.LeftUpperArm),
		( -1, 3, (int)HumanBodyBones.RightUpperArm),
		( -1, 3, (int)HumanBodyBones.LeftLowerArm),
		( -1, 3, (int)HumanBodyBones.RightLowerArm),
		( -1, 3, (int)HumanBodyBones.LeftHand),
		( -1, 3, (int)HumanBodyBones.RightHand),

		( 69, 1, (int)HumanBodyBones.LeftToes), // toe > eye in mass
		( -1, 1, (int)HumanBodyBones.RightToes),
		( -1, 2, (int)HumanBodyBones.LeftEye),
		( -1, 2, (int)HumanBodyBones.RightEye),
		( -1, 2, (int)HumanBodyBones.Jaw),

		// 77 ~ 89: reserved

		( 90, 2, (int)HumanBodyBones.LeftThumbProximal),
		( -1, 1, (int)HumanBodyBones.LeftThumbIntermediate),
		( -1, 1, (int)HumanBodyBones.LeftThumbDistal),
		( -1, 2, (int)HumanBodyBones.LeftIndexProximal),
		( -1, 1, (int)HumanBodyBones.LeftIndexIntermediate),
		( -1, 1, (int)HumanBodyBones.LeftIndexDistal),
		( -1, 2, (int)HumanBodyBones.LeftMiddleProximal),
		( -1, 1, (int)HumanBodyBones.LeftMiddleIntermediate),
		( -1, 1, (int)HumanBodyBones.LeftMiddleDistal),
		( -1, 2, (int)HumanBodyBones.LeftRingProximal),
		( -1, 1, (int)HumanBodyBones.LeftRingIntermediate),
		( -1, 1, (int)HumanBodyBones.LeftRingDistal),
		( -1, 2, (int)HumanBodyBones.LeftLittleProximal),
		( -1, 1, (int)HumanBodyBones.LeftLittleIntermediate),
		( -1, 1, (int)HumanBodyBones.LeftLittleDistal),

		(110, 2, (int)HumanBodyBones.RightThumbProximal),
		( -1, 1, (int)HumanBodyBones.RightThumbIntermediate),
		( -1, 1, (int)HumanBodyBones.RightThumbDistal),
		( -1, 2, (int)HumanBodyBones.RightIndexProximal),
		( -1, 1, (int)HumanBodyBones.RightIndexIntermediate),
		( -1, 1, (int)HumanBodyBones.RightIndexDistal),
		( -1, 2, (int)HumanBodyBones.RightMiddleProximal),
		( -1, 1, (int)HumanBodyBones.RightMiddleIntermediate),
		( -1, 1, (int)HumanBodyBones.RightMiddleDistal),
		( -1, 2, (int)HumanBodyBones.RightRingProximal),
		( -1, 1, (int)HumanBodyBones.RightRingIntermediate),
		( -1, 1, (int)HumanBodyBones.RightRingDistal),
		( -1, 2, (int)HumanBodyBones.RightLittleProximal),
		( -1, 1, (int)HumanBodyBones.RightLittleIntermediate),
		( -1, 1, (int)HumanBodyBones.RightLittleDistal),

		// 130 ~ 144: reserved
	};
}
}