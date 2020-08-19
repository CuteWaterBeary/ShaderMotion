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
	static readonly Regex Constrained = new Regex(@"Jaw|Toes|Eye|Proximal|Intermediate|Distal$");

	public Quaternion preQ, postQ; // bone.localRotation * postQ == preQ * muscleQ(sign * angle)
	public float sign;
	public Vector3 min, max; // 0 = locked, NaN = non-human or affected by twist distribution
	public BoneAxes(Transform bone) {
		sign  = 1;
		postQ = Quaternion.LookRotation(Vector3.right, Vector3.forward); // Unity's convention: Y-axis = twist
		preQ  = bone.localRotation * postQ;
		min   = Vector3.zero * float.NaN;
		max   = Vector3.zero * float.NaN;
	}
	public BoneAxes(Avatar avatar, HumanBodyBones humanBone) {
		var sign3 = (Vector3)GetLimitSign.Invoke(avatar, new object[]{humanBone});
		var signQ = Quaternion.LookRotation(new Vector3(0, 0, sign3.x*sign3.y),
											new Vector3(0, sign3.x*sign3.z, 0));
		// bake non-uniform sign into uniform sign:
		// muscleQ(sign3 * angle) == det(sign3)flip(sign3) * muscleQ(det(sign3) * angle) * det(sign3)flip(sign3)
		preQ  = (Quaternion)GetPreRotation.Invoke(avatar, new object[]{humanBone}) * signQ;
		postQ = (Quaternion)GetPostRotation.Invoke(avatar, new object[]{humanBone}) * signQ;
		sign  = sign3.x*sign3.y*sign3.z;
		// rotation min/max
		min = max = Vector3.zero * (Constrained.IsMatch(HumanTrait.BoneName[(int)humanBone]) ? 0 : float.NaN);
		for(int i=0; i<3; i++) {
			var muscle = HumanTrait.MuscleFromBone((int)humanBone, i);
			if(muscle >= 0) { // use global limits since most avatars keep default values
				min[i] = HumanTrait.GetMuscleDefaultMin(muscle);
				max[i] = HumanTrait.GetMuscleDefaultMax(muscle);
			}
		}
		// hips uses rootQ
		if(humanBone == HumanBodyBones.Hips)
			ClearPreQ();
	}
	public void ClearPreQ() {
		// use rootQ instead of muscleQ: bone.rotation * postQ == rootQ * preQ
		postQ *= Quaternion.Inverse(preQ);
		preQ   = Quaternion.identity;
		sign   = 1;
	}
}
}