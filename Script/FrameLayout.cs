using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using Path = System.IO.Path;
using UnityEngine;

namespace ShaderMotion {
public class FrameLayout {
	public int[][] channels;
	public int[]   baseIndices;
	public KeyValuePair<string,int>[] shapeIndices = null;
	public FrameLayout(HumanUtil.Armature arm, Dictionary<int,int> overrides=null) {
		channels = new int[arm.bones.Length][];
		baseIndices = new int[arm.bones.Length];
		var slot = 0;
		for(int i=0; i<arm.bones.Length; i++) {
			var chan = new List<int>();
			if(arm.bones[i] && arm.parents[i] < 0)
				chan.AddRange(Enumerable.Range(3, 12));
			else
				for(int j=0; j<3; j++)
					if(!(arm.axes[i].max[j] == 0))
						chan.Add(j);
			channels[i] = chan.ToArray();

			if(overrides != null && overrides.ContainsKey(i))
				slot = overrides[i];
			baseIndices[i] = slot;
			slot += chan.Count;
		}
	}

	public static HumanBodyBones[] defaultHumanBones = new []{
		// roughly ordered by BoneDefaultHierarchyMass
		// 0: first column
		HumanBodyBones.Hips,
		HumanBodyBones.Spine,
		HumanBodyBones.Chest,
		HumanBodyBones.UpperChest,
		HumanBodyBones.Neck,
		HumanBodyBones.Head,
		// 27
		HumanBodyBones.LeftUpperLeg,
		HumanBodyBones.RightUpperLeg,
		HumanBodyBones.LeftLowerLeg,
		HumanBodyBones.RightLowerLeg,
		HumanBodyBones.LeftFoot,
		HumanBodyBones.RightFoot,
		// 45: second column
		HumanBodyBones.LeftShoulder,
		HumanBodyBones.RightShoulder,
		HumanBodyBones.LeftUpperArm,
		HumanBodyBones.RightUpperArm,
		HumanBodyBones.LeftLowerArm,
		HumanBodyBones.RightLowerArm,
		HumanBodyBones.LeftHand,
		HumanBodyBones.RightHand,
		// 69: toe > eye in mass
		HumanBodyBones.LeftToes,
		HumanBodyBones.RightToes,
		HumanBodyBones.LeftEye,
		HumanBodyBones.RightEye,
		HumanBodyBones.Jaw,
		// 77
		HumanBodyBones.LeftThumbProximal,
		HumanBodyBones.LeftThumbIntermediate,
		HumanBodyBones.LeftThumbDistal,
		HumanBodyBones.LeftIndexProximal,
		HumanBodyBones.LeftIndexIntermediate,
		HumanBodyBones.LeftIndexDistal,
		HumanBodyBones.LeftMiddleProximal,
		HumanBodyBones.LeftMiddleIntermediate,
		HumanBodyBones.LeftMiddleDistal,
		HumanBodyBones.LeftRingProximal,
		HumanBodyBones.LeftRingIntermediate,
		HumanBodyBones.LeftRingDistal,
		HumanBodyBones.LeftLittleProximal,
		HumanBodyBones.LeftLittleIntermediate,
		HumanBodyBones.LeftLittleDistal,
		// 97
		HumanBodyBones.RightThumbProximal,
		HumanBodyBones.RightThumbIntermediate,
		HumanBodyBones.RightThumbDistal,
		HumanBodyBones.RightIndexProximal,
		HumanBodyBones.RightIndexIntermediate,
		HumanBodyBones.RightIndexDistal,
		HumanBodyBones.RightMiddleProximal,
		HumanBodyBones.RightMiddleIntermediate,
		HumanBodyBones.RightMiddleDistal,
		HumanBodyBones.RightRingProximal,
		HumanBodyBones.RightRingIntermediate,
		HumanBodyBones.RightRingDistal,
		HumanBodyBones.RightLittleProximal,
		HumanBodyBones.RightLittleIntermediate,
		HumanBodyBones.RightLittleDistal,
		// 117
	};
	public static Dictionary<int,int> defaultOverrides = new Dictionary<int,int>{
		{25, 90},
	};
}
}