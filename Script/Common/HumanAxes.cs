using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ShaderMotion {
public struct HumanAxes {
	public float sign;
	public Quaternion postQ; // tip == bone.position + bone.rotation * postQ * Vector3.right * limit.axisLength
	public Quaternion preQ;  // bone.localRotation * postQ == preQ * SwingTwist(sign * angles)
	public HumanLimit limit; // limit.max == NaN denotes non-muscle axes of Jaw/Toes/Eye/Finger
	public HumanAxes(Transform bone, Vector3 dir=new Vector3()) {
		if(dir == Vector3.zero) { // guess bone direction
			foreach(Transform c in bone)
				dir += c.localPosition;
			if(dir == Vector3.zero)
				dir = -bone.InverseTransformPoint(bone.parent.position);
			dir = bone.InverseTransformVector(Vector3.down);
		}
		sign  = 1;
		postQ = Quaternion.FromToRotation(Vector3.right, dir);
		preQ  = bone.localRotation * postQ;
		limit = new HumanLimit{ // TODO
			min = float.NegativeInfinity * Vector3.one,
			max = float.PositiveInfinity * Vector3.one,
		};
	}
	public HumanAxes(Avatar avatar, HumanBodyBones humanBone) {
		// bake non-uniform sign into uniform sign
		var sign3 = GetLimitSignFull(avatar, humanBone);
		var signQ = Quaternion.LookRotation(new Vector3(0, 0, sign3.x*sign3.y), new Vector3(0, sign3.x*sign3.z, 0));
		sign  = sign3.x*sign3.y*sign3.z;
		preQ  = (Quaternion)GetPreRotation .Invoke(avatar, new object[]{humanBone}) * signQ;
		postQ = (Quaternion)GetPostRotation.Invoke(avatar, new object[]{humanBone}) * signQ;
		limit = GetHumanLimit(avatar, humanBone, exposeNonMuscleAxes.Contains(humanBone) ? float.PositiveInfinity : float.NaN);
		limit.axisLength *= sign3.y*sign3.z;
		// zyroll is not handled
		var zyRoll = (Quaternion)GetZYRoll.Invoke(avatar, new object[]{humanBone, Vector3.zero});
		Debug.Assert(zyRoll == Quaternion.identity, $"{humanBone} has non-trivial zyRoll: {zyRoll}");
	}
	public static Quaternion SwingTwist(Vector3 degree) {
		var degreeYZ = new Vector3(0, degree.y, degree.z);
		return Quaternion.AngleAxis(degreeYZ.magnitude, degreeYZ.normalized)
				* Quaternion.AngleAxis(degree.x, new Vector3(1,0,0));
	}

	static HumanLimit GetHumanLimit(Avatar avatar, HumanBodyBones humanBone, float missing=float.NaN) {
		var min = -missing * new Vector3(1,1,1);
		var max = +missing * new Vector3(1,1,1);
		for(int i=0; i<3; i++) {
			var m = HumanTrait.MuscleFromBone((int)humanBone, i);
			if(m >= 0)
				(min[i], max[i]) = (HumanTrait.GetMuscleDefaultMin(m), HumanTrait.GetMuscleDefaultMax(m));
		}
		return new HumanLimit{
			min = min, max = max, center = Vector3.zero, useDefaultValues = true,
			axisLength = (float)GetAxisLength.Invoke(avatar, new object[]{humanBone}),
		};
	}
	static Vector3 GetLimitSignFull(Avatar avatar, HumanBodyBones humanBone) {
		var sign = Vector3.zero;
		for(var b = humanBone; (int)b >= 0; ) {
			var s = (Vector3)GetLimitSign.Invoke(avatar, new object[]{b});
			for(int i=0; i<3; i++)
				if(HumanTrait.MuscleFromBone((int)b, i) < 0)
					s[i] = 0;
				else if(sign[i] == 0)
					sign[i] = s[i];

			if(s.x*s.y*s.z != 0) {
				for(int i=0; i<3 && (sign.x*sign.y*sign.z) != (s.x*s.y*s.z); i++)
					if(HumanTrait.MuscleFromBone((int)humanBone, i) < 0)
						sign[i] *= -1;
				return sign;
			}

			b = b == HumanBodyBones.LeftShoulder  ? HumanBodyBones.LeftUpperArm :
				b == HumanBodyBones.RightShoulder ? HumanBodyBones.RightUpperArm :
				(HumanBodyBones)HumanTrait.GetParentBone((int)b);
		}
		return new Vector3(1, 1, 1);
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
}
}