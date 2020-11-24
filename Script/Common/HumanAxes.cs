using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ShaderMotion {
public struct HumanAxes {
	public float sign;
	public Quaternion postQ; // tip == bone.position + bone.rotation * postQ * Vector3.right * limit.axisLength
	public Quaternion preQ;  // bone.localRotation * postQ == preQ * SwingTwist(sign * angles)
	public HumanLimit limit;
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
		limit = new HumanLimit{};
	}
	public HumanAxes(Avatar avatar, HumanBodyBones humanBone) {
		// bake non-uniform sign into quaternions
		var sign3 = GetLimitSignFull(avatar, humanBone);
		var signQ = Quaternion.LookRotation(new Vector3(0, 0, sign3.x*sign3.y), new Vector3(0, sign3.x*sign3.z, 0));
		sign  = sign3.x*sign3.y*sign3.z;
		preQ  = (Quaternion)GetPreRotation .Invoke(avatar, new object[]{humanBone}) * signQ;
		postQ = (Quaternion)GetPostRotation.Invoke(avatar, new object[]{humanBone}) * signQ;
		limit = GetHumanLimit(avatar, humanBone);
		limit.axisLength *= sign3.y*sign3.z;
	}
	public static Quaternion SwingTwist(Vector3 degree) {
		var degreeYZ = new Vector3(0, degree.y, degree.z);
		return Quaternion.AngleAxis(degreeYZ.magnitude, degreeYZ.normalized)
				* Quaternion.AngleAxis(degree.x, new Vector3(1,0,0));
	}

	static HumanLimit GetHumanLimit(Avatar avatar, HumanBodyBones humanBone) {
		var limit = new HumanLimit{useDefaultValues = true};
		#if UNITY_EDITOR // this can be improved when avatar.humanDescription is exposed in unity 2019
			var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(avatar)) as ModelImporter;
			var human = (importer?.humanDescription)?.human;
			if(human != null)
				for(int i=0; i<human.Length; i++)
					if(human[i].humanName == HumanTrait.BoneName[(int)humanBone]) {
						limit = human[i].limit;
						break;
					}
		#endif
		limit.axisLength = (float)GetAxisLength.Invoke(avatar, new object[]{humanBone}); // not in humanDescription
		if(limit.useDefaultValues) {
			Vector3 min = new Vector3(), max = new Vector3();
			for(int i=0; i<3; i++) {
				var m = HumanTrait.MuscleFromBone((int)humanBone, i);
				min[i] = m >= 0 ? HumanTrait.GetMuscleDefaultMin(m) : 0; // invalid input will crash
				max[i] = m >= 0 ? HumanTrait.GetMuscleDefaultMax(m) : 0;
			}
			limit.min = min;
			limit.max = max;
		}
		return limit;
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
	static readonly MethodInfo GetPreRotation  = typeof(Avatar).GetMethod("GetPreRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetPostRotation = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetLimitSign    = typeof(Avatar).GetMethod("GetLimitSign", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetZYRoll       = typeof(Avatar).GetMethod("GetZYRoll", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo GetAxisLength   = typeof(Avatar).GetMethod("GetAxisLength", BindingFlags.NonPublic | BindingFlags.Instance);
}
}