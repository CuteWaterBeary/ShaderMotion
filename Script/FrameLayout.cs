using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using Path = System.IO.Path;
using UnityEngine;
using System.Text.RegularExpressions;

namespace ShaderMotion {
public class FrameLayout {
	public struct ShapeIndex {
		public string shape;
		public int index;
		public float weight;
	}
	public int[][] channels;
	public int[]   baseIndices;
	public List<ShapeIndex> shapeIndices = new List<ShapeIndex>();
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
		// roughly ordered by HumanTrait.GetBoneDefaultHierarchyMass
		// 0: spine
		HumanBodyBones.Hips,
		HumanBodyBones.Spine,
		HumanBodyBones.Chest,
		HumanBodyBones.UpperChest,
		HumanBodyBones.Neck,
		HumanBodyBones.Head,
		// 27: legs
		HumanBodyBones.LeftUpperLeg,
		HumanBodyBones.RightUpperLeg,
		HumanBodyBones.LeftLowerLeg,
		HumanBodyBones.RightLowerLeg,
		HumanBodyBones.LeftFoot,
		HumanBodyBones.RightFoot,
		// 45: arms
		HumanBodyBones.LeftShoulder,
		HumanBodyBones.RightShoulder,
		HumanBodyBones.LeftUpperArm,
		HumanBodyBones.RightUpperArm,
		HumanBodyBones.LeftLowerArm,
		HumanBodyBones.RightLowerArm,
		HumanBodyBones.LeftHand,
		HumanBodyBones.RightHand,
		// 69: misc (toe > eye in mass)
		HumanBodyBones.LeftToes,
		HumanBodyBones.RightToes,
		HumanBodyBones.LeftEye,
		HumanBodyBones.RightEye,
		HumanBodyBones.Jaw,
		// 77~90: reserved
		// 90: left-hand fingers
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
		// 110: right-hand fingers
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
	};
	public static Dictionary<int,int> defaultOverrides = new Dictionary<int,int>{
		{25, 90},
	};

	public void AddEncoderVisemeShapes(Mesh mesh=null, int baseIndex=80) {
		foreach(var vc in visemeTable)
			for(int i=0; i<3; i++) {
				var name = "v_"+vc.Key;
				shapeIndices.Add(new ShapeIndex{shape=name, index=baseIndex+i, weight=vc.Value[i]});
			}
	}
	public void AddDecoderVisemeShapes(Mesh mesh=null, int baseIndex=80) {
		var shapeNames = new List<string>();
		if(mesh)
			for(int i=0; i<mesh.blendShapeCount; i++)
				shapeNames.Add(mesh.GetBlendShapeName(i));

		foreach(var vc in visemeTable)
			for(int i=0; i<3; i++)
				if(vc.Value[i] == 1) {
					var name = searchVisemeName(shapeNames, vc.Key);
					shapeIndices.Add(new ShapeIndex{shape=name, index=baseIndex+i, weight=vc.Value[i]});
				}
	}
	// express visemes as weighted sums of A/CH/O, widely used by CATS
	// https://github.com/GiveMeAllYourCats/cats-blender-plugin/blob/master/tools/viseme.py#L102
	static KeyValuePair<string, Vector3>[] visemeTable = new KeyValuePair<string, Vector3>[]{
		new KeyValuePair<string, Vector3>("sil", Vector3.zero),
		new KeyValuePair<string, Vector3>("PP", new Vector3(0.0f, 0.0f, 0.0f)),
		new KeyValuePair<string, Vector3>("FF", new Vector3(0.2f, 0.4f, 0.0f)),
		new KeyValuePair<string, Vector3>("TH", new Vector3(0.4f, 0.0f, 0.15f)),
		new KeyValuePair<string, Vector3>("DD", new Vector3(0.3f, 0.7f, 0.0f)),
		new KeyValuePair<string, Vector3>("kk", new Vector3(0.7f, 0.4f, 0.0f)),
		new KeyValuePair<string, Vector3>("CH", new Vector3(0.0f, 1.0f, 0.0f)),
		new KeyValuePair<string, Vector3>("SS", new Vector3(0.0f, 0.8f, 0.0f)),
		new KeyValuePair<string, Vector3>("nn", new Vector3(0.2f, 0.7f, 0.0f)),
		new KeyValuePair<string, Vector3>("RR", new Vector3(0.0f, 0.5f, 0.3f)),
		new KeyValuePair<string, Vector3>("aa", new Vector3(1.0f, 0.0f, 0.0f)),
		new KeyValuePair<string, Vector3>("E",  new Vector3(0.0f, 0.7f, 0.3f)),
		new KeyValuePair<string, Vector3>("ih", new Vector3(0.5f, 0.2f, 0.0f)),
		new KeyValuePair<string, Vector3>("oh", new Vector3(0.2f, 0.0f, 0.8f)),
		new KeyValuePair<string, Vector3>("ou", new Vector3(0.0f, 0.0f, 1.0f)),
	};
	string searchVisemeName(IEnumerable<string> names, string viseme) {
		var r = new Regex($@"\bv_{viseme}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		foreach(var name in names)
			if(r.IsMatch(name))
				return name;
		r = new Regex($@"\b{viseme}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		foreach(var name in names)
			if(r.IsMatch(name))
				return name;
		return $"v_{viseme}";
    }
}
}