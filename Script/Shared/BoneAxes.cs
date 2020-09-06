using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ShaderMotion {
public struct BoneAxes {
	public Quaternion preQ, postQ; // bone.localRotation * postQ == preQ * SwingTwist(sign * angles)
	public float sign, signX;
	public Vector3 min, max; // NaN == axis unavailable (Jaw|Toes|Eye|Proximal|Intermediate|Distal)
	public BoneAxes(Transform bone, Vector3 dir=new Vector3()) {
		min = float.NegativeInfinity * new Vector3(1,1,1);
		max = float.PositiveInfinity * new Vector3(1,1,1);
		if(dir == Vector3.zero) { // guess bone direction
			foreach(Transform c in bone)
				dir += c.localPosition;
			if(dir == Vector3.zero)
				dir = -bone.InverseTransformPoint(bone.parent.position);
			dir = bone.InverseTransformVector(Vector3.down);
		}
		postQ = Quaternion.FromToRotation(Vector3.right, dir);
		preQ  = bone.localRotation * postQ;
		sign  = 1;
		signX = 1;
	}
	public BoneAxes(Avatar avatar, HumanBodyBones humanBone) {
		min = (exposeHiddenAxes.Contains(humanBone) ? float.NegativeInfinity : float.NaN) * new Vector3(1,1,1);
		max = (exposeHiddenAxes.Contains(humanBone) ? float.PositiveInfinity : float.NaN) * new Vector3(1,1,1);
		for(int j=0; j<3; j++)
			if(MuscleFromBone[(int)humanBone, j] >= 0) {
				min[j] = HumanTrait.GetMuscleDefaultMin(MuscleFromBone[(int)humanBone, j]);
				max[j] = HumanTrait.GetMuscleDefaultMax(MuscleFromBone[(int)humanBone, j]);
			}
		// bake non-uniform sign into uniform sign:
		// SwingTwist(sign3 * angles) == det(sign3)flip(sign3) * SwingTwist(det(sign3) * angles) * det(sign3)flip(sign3)
		var sign3 = GetAxesSign(avatar, (int)humanBone);
		var signQ = Quaternion.LookRotation(new Vector3(0, 0, sign3.x*sign3.y), new Vector3(0, sign3.x*sign3.z, 0));
		preQ  = (Quaternion)GetPreRotation.Invoke(avatar, new object[]{humanBone}) * signQ;
		postQ = (Quaternion)GetPostRotation.Invoke(avatar, new object[]{humanBone}) * signQ;
		sign  = sign3.x*sign3.y*sign3.z;
		signX = sign3.y*sign3.z;
	}
	public void ClearPreQ() {
		// use rootQ instead of SwingTwist: bone.rotation * postQ == rootQ * preQ
		postQ *= Quaternion.Inverse(preQ);
		preQ   = Quaternion.identity;
		sign   = 1;
	}

	static Vector3 GetAxesSign(Avatar avatar, int humanBone) {
		var par  = (Vector3)GetLimitSign.Invoke(avatar, new object[]{parentAxes[humanBone]});
		var sign = (Vector3)GetLimitSign.Invoke(avatar, new object[]{humanBone});
		// copy missing components from parent
		for(int i=0; i<3; i++)
			if(MuscleFromBone[humanBone, i] < 0)
				sign[i] = par[i];
		// ensure same handedness as parent
		for(int i=0; i<3; i++)
			if(MuscleFromBone[humanBone, i] < 0)
				sign[i] *= (sign.x*sign.y*sign.z) * (par.x*par.y*par.z);
		return sign;
	}
	static readonly MethodInfo GetPreRotation  = typeof(Avatar).GetMethod("GetPreRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetLimitSign    = typeof(Avatar).GetMethod("GetLimitSign", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly int[,] MuscleFromBone = new int[HumanTrait.BoneCount, 3];
	static readonly int[]  parentAxes = new int[HumanTrait.BoneCount];
	static readonly HashSet<HumanBodyBones> exposeHiddenAxes = new HashSet<HumanBodyBones>{
		HumanBodyBones.Hips,
		HumanBodyBones.LeftLowerLeg,	HumanBodyBones.RightLowerLeg,
		HumanBodyBones.LeftFoot,		HumanBodyBones.RightFoot,
		HumanBodyBones.LeftShoulder,	HumanBodyBones.RightShoulder,
		HumanBodyBones.LeftLowerArm,	HumanBodyBones.RightLowerArm,
		HumanBodyBones.LeftHand,		HumanBodyBones.RightHand,
	};
	static BoneAxes() {
		for(int i=0; i<HumanTrait.BoneCount; i++) {
			for(int j=0; j<3; j++)
				MuscleFromBone[i, j] = HumanTrait.MuscleFromBone(i, j);
			parentAxes[i] = Enumerable.Range(0, 3).All(j => MuscleFromBone[i, j] >= 0) ? i : -1;
		}
		parentAxes[(int)HumanBodyBones.Hips] 			= (int)HumanBodyBones.Hips;
		parentAxes[(int)HumanBodyBones.LeftShoulder]  	= (int)HumanBodyBones.LeftUpperArm;
		parentAxes[(int)HumanBodyBones.RightShoulder] 	= (int)HumanBodyBones.RightUpperArm;
		for(int i=0; i<HumanTrait.BoneCount; i++) {
			var p = i;
			while(parentAxes[p] < 0)
				p = HumanTrait.GetParentBone(p);
			parentAxes[i] = parentAxes[p];
		}
	}
}
}