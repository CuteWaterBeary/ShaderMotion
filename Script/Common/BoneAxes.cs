using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ShaderMotion {
public struct BoneAxes {
	public Quaternion preQ, postQ; // bone.localRotation * postQ == preQ * SwingTwist(sign * angles)
	public float sign, length; // axisEnd == bone.position + bone.rotation * postQ * Vector3.right * length
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
		length = 0; // TODO
	}
	public BoneAxes(Avatar avatar, HumanBodyBones humanBone) {
		min = (exposeNonMuscleAxes.Contains(humanBone) ? float.NegativeInfinity : float.NaN) * new Vector3(1,1,1);
		max = (exposeNonMuscleAxes.Contains(humanBone) ? float.PositiveInfinity : float.NaN) * new Vector3(1,1,1);
		var sign3 = GetLimit(avatar, (int)humanBone, ref min, ref max);
		// bake non-uniform sign into uniform sign
		var signQ = Quaternion.LookRotation(new Vector3(0, 0, sign3.x*sign3.y), new Vector3(0, sign3.x*sign3.z, 0));
		preQ   = (Quaternion)GetPreRotation .Invoke(avatar, new object[]{humanBone}) * signQ;
		postQ  = (Quaternion)GetPostRotation.Invoke(avatar, new object[]{humanBone}) * signQ;
		length = (float)GetAxisLength.Invoke(avatar, new object[]{humanBone}) * (sign3.y*sign3.z);
		sign   = sign3.x*sign3.y*sign3.z;
		// zyroll is not handled
		var zyRoll = (Quaternion)GetZYRoll.Invoke(avatar, new object[]{humanBone, Vector3.zero});
		Debug.Assert(zyRoll == Quaternion.identity, $"{humanBone} has non-trivial zyRoll: {zyRoll}");
	}
	public static Quaternion SwingTwist(Vector3 degree) {
		var degreeYZ = new Vector3(0, degree.y, degree.z);
		return Quaternion.AngleAxis(degreeYZ.magnitude, degreeYZ.normalized)
				* Quaternion.AngleAxis(degree.x, new Vector3(1,0,0));
	}

	static Vector3 GetLimit(Avatar avatar, int humanBone, ref Vector3 min, ref Vector3 max) {
		var par  = (Vector3)GetLimitSign.Invoke(avatar, new object[]{parentAxes[humanBone]});
		var sign = (Vector3)GetLimitSign.Invoke(avatar, new object[]{humanBone});
		for(int i=0; i<3; i++) {
			var m = HumanTrait.MuscleFromBone((int)humanBone, i);
			if(m >= 0)
				(min[i], max[i]) = (HumanTrait.GetMuscleDefaultMin(m), HumanTrait.GetMuscleDefaultMax(m));
			else
				sign[i] = par[i];
		}
		for(int i=0; i<3; i++) // enforce same handedness
			if(HumanTrait.MuscleFromBone(humanBone, i) < 0)
				sign[i] *= (sign.x*sign.y*sign.z) * (par.x*par.y*par.z);
		return sign;
	}
	static readonly MethodInfo GetPreRotation  = typeof(Avatar).GetMethod("GetPreRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetLimitSign    = typeof(Avatar).GetMethod("GetLimitSign", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetZYRoll       = typeof(Avatar).GetMethod("GetZYRoll", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetAxisLength   = typeof(Avatar).GetMethod("GetAxisLength", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly HashSet<HumanBodyBones> exposeNonMuscleAxes = new HashSet<HumanBodyBones>{
		HumanBodyBones.Hips,
		HumanBodyBones.LeftLowerLeg,	HumanBodyBones.RightLowerLeg,
		HumanBodyBones.LeftFoot,		HumanBodyBones.RightFoot,
		HumanBodyBones.LeftShoulder,	HumanBodyBones.RightShoulder,
		HumanBodyBones.LeftLowerArm,	HumanBodyBones.RightLowerArm,
		HumanBodyBones.LeftHand,		HumanBodyBones.RightHand,
	};
	static readonly int[] parentAxes = Enumerable.Range(0, HumanTrait.BoneCount).Select(i => {
		if(i == (int)HumanBodyBones.LeftShoulder)
			i = (int)HumanBodyBones.LeftUpperArm;
		else if(i == (int)HumanBodyBones.RightShoulder)
			i = (int)HumanBodyBones.RightUpperArm;
		else while(i != (int)HumanBodyBones.Hips && !Enumerable.Range(0, 3).All(
					j => HumanTrait.MuscleFromBone(i, j) >= 0))
						i = HumanTrait.GetParentBone(i);
		return i;
	}).ToArray();
}
}