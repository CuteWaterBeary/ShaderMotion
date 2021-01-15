using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ShaderMotion {
public struct HumanAxes {
	public Quaternion postQ; // tip == bone.position + bone.rotation * postQ * Vector3.right * limit.axisLength
	public Quaternion preQ;  // bone.localRotation * postQ == preQ * SwingTwist(sign * degrees)
	public Vector3 sign;     // abs(sign) != 1 is possible for custom limit range
	public HumanAxes(Transform bone, Vector3 dir=new Vector3()) {
		if(dir == Vector3.zero) { // guess bone direction
			foreach(Transform c in bone)
				dir += c.localPosition;
			if(dir == Vector3.zero)
				dir = -bone.InverseTransformPoint(bone.parent.position);
			dir = bone.InverseTransformVector(Vector3.down);
		}
		postQ = Quaternion.FromToRotation(Vector3.right, dir);
		preQ  = bone.localRotation * postQ;
		sign  = Vector3.one;
	}
	public HumanAxes(Avatar avatar, HumanBodyBones humanBone) {
		postQ = (Quaternion)GetPostRotation.Invoke(avatar, new object[]{humanBone});
		preQ  = (Quaternion)GetPreRotation .Invoke(avatar, new object[]{humanBone});
		sign  = GetLimitSignScaled(avatar, humanBone);
	}
	public static Quaternion SwingTwist(Vector3 degree) {
		var degreeYZ = new Vector3(0, degree.y, degree.z);
		return Quaternion.AngleAxis(degreeYZ.magnitude, degreeYZ.normalized)
				* Quaternion.AngleAxis(degree.x, new Vector3(1,0,0));
	}

	// this method is based on GetLimitSign with two modifications:
	// 1. non-muscle axis sign is chosen to match ancestor's, while GetLimitSign always returns +1
	// 2. sign is scaled to reflect the limit range change from its default value
	static Vector3 GetLimitSignScaled(Avatar avatar, HumanBodyBones humanBone) {
		var human = default(HumanBone[]);
		#if UNITY_EDITOR // might be improved when unity 2019 exposes avatar.humanDescription
			human = ((AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(avatar)) as ModelImporter)
						?.humanDescription)?.human;
		#endif

		var sign = Vector3.zero;
		for(var b = humanBone; (int)b >= 0; ) {
			var limit = GetHumanLimit(human, b);
			var s = (Vector3)GetLimitSign.Invoke(avatar, new object[]{b});
			for(int i=0; i<3; i++) {
				var m = HumanTrait.MuscleFromBone((int)b, i);
				if(m < 0)
					s[i] = 0;
				else if(sign[i] == 0) {
					sign[i] = s[i];
					if(!limit.useDefaultValues)
						sign[i] *= (limit.max[i]-limit.min[i])
								/ (HumanTrait.GetMuscleDefaultMax(m)-HumanTrait.GetMuscleDefaultMin(m));
				}
			}

			if(s.x*s.y*s.z != 0) {
				// match orientation
				for(int i=0; i<3 && (sign.x*sign.y*sign.z) != (s.x*s.y*s.z); i++)
					if(HumanTrait.MuscleFromBone((int)humanBone, i) < 0)
						sign[i] *= -1;
				return sign;
			}

			b = b == HumanBodyBones.LeftShoulder  ? HumanBodyBones.LeftUpperArm :
				b == HumanBodyBones.RightShoulder ? HumanBodyBones.RightUpperArm :
				(HumanBodyBones)HumanTrait.GetParentBone((int)b);
		}
		return Vector3.one;
	}
	static HumanLimit GetHumanLimit(HumanBone[] human, HumanBodyBones bone) {
		if(human != null)
			foreach(var hb in human)
				if(hb.humanName == HumanTrait.BoneName[(int)bone])
					return hb.limit;
		return new HumanLimit{useDefaultValues = true};
	}
	static readonly MethodInfo GetPreRotation  = typeof(Avatar).GetMethod("GetPreRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetLimitSign    = typeof(Avatar).GetMethod("GetLimitSign", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetZYRoll       = typeof(Avatar).GetMethod("GetZYRoll", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetAxisLength   = typeof(Avatar).GetMethod("GetAxisLength", BindingFlags.NonPublic | BindingFlags.Instance);
}
}