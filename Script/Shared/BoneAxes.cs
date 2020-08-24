using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ShaderMotion {
public struct BoneAxes {
	static readonly MethodInfo GetPreRotation  = typeof(Avatar).GetMethod("GetPreRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetLimitSign    = typeof(Avatar).GetMethod("GetLimitSign", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly Dictionary<HumanBodyBones,HumanBodyBones> fixImplicitAxis = new Dictionary<HumanBodyBones,HumanBodyBones>{
		{HumanBodyBones.LeftLowerLeg,	HumanBodyBones.LeftUpperLeg},
		{HumanBodyBones.LeftFoot,		HumanBodyBones.LeftUpperLeg},
		{HumanBodyBones.LeftShoulder,	HumanBodyBones.LeftUpperArm},
		{HumanBodyBones.LeftLowerArm,	HumanBodyBones.LeftUpperArm},
		{HumanBodyBones.LeftHand,		HumanBodyBones.LeftUpperArm},
		{HumanBodyBones.RightLowerLeg,	HumanBodyBones.RightUpperLeg},
		{HumanBodyBones.RightFoot,		HumanBodyBones.RightUpperLeg},
		{HumanBodyBones.RightShoulder,	HumanBodyBones.RightUpperArm},
		{HumanBodyBones.RightLowerArm,	HumanBodyBones.RightUpperArm},
		{HumanBodyBones.RightHand,		HumanBodyBones.RightUpperArm},
	};

	public Quaternion preQ, postQ; // bone.localRotation * postQ == preQ * muscleQ(sign * angle)
	public float sign, signX;
	public Vector3 min, max; // NaN == axis is locked (Jaw|Toes|Eye|Proximal|Intermediate|Distal)
	public BoneAxes(Transform bone) {
		sign  = 1;
		signX = 1;
		min   = float.NegativeInfinity * new Vector3(1,1,1);
		max   = float.PositiveInfinity * new Vector3(1,1,1);
		postQ = Quaternion.LookRotation(Vector3.right, Vector3.forward); // Unity's convention: Y-axis = twist
		preQ  = bone.localRotation * postQ;
	}
	public BoneAxes(Avatar avatar, HumanBodyBones humanBone) {
		var sign3 = (Vector3)GetLimitSign.Invoke(avatar, new object[]{humanBone});
		min = max = float.NaN * Vector3.zero;
		for(int i=0; i<3; i++) {
			var muscle = HumanTrait.MuscleFromBone((int)humanBone, i);
			if(muscle >= 0) {
				min[i] = HumanTrait.GetMuscleDefaultMin(muscle);
				max[i] = HumanTrait.GetMuscleDefaultMax(muscle);
			} else if(humanBone == HumanBodyBones.Hips || fixImplicitAxis.ContainsKey(humanBone)) {
				min[i] = float.NegativeInfinity;
				max[i] = float.PositiveInfinity;
				// if an axis is implicitly controlled by twist distribution, fix its sign
				if(humanBone != HumanBodyBones.Hips) {
					var s = (Vector3)GetLimitSign.Invoke(avatar, new object[]{fixImplicitAxis[humanBone]});
					sign3[i] *= (sign3.x*sign3.y*sign3.z) * (s.x*s.y*s.z);
				}
			}
		}
		// bake non-uniform sign into uniform sign:
		// muscleQ(sign3 * angle) == det(sign3)flip(sign3) * muscleQ(det(sign3) * angle) * det(sign3)flip(sign3)
		var signQ = Quaternion.LookRotation(new Vector3(0, 0, sign3.x*sign3.y), new Vector3(0, sign3.x*sign3.z, 0));
		preQ  = (Quaternion)GetPreRotation.Invoke(avatar, new object[]{humanBone}) * signQ;
		postQ = (Quaternion)GetPostRotation.Invoke(avatar, new object[]{humanBone}) * signQ;
		sign  = sign3.x*sign3.y*sign3.z;
		signX = sign3.y*sign3.z;
	}
	public void ClearPreQ() {
		// use rootQ instead of muscleQ: bone.rotation * postQ == rootQ * preQ
		postQ *= Quaternion.Inverse(preQ);
		preQ   = Quaternion.identity;
		sign   = 1;
	}
}
}