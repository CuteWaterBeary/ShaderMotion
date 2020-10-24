using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;

namespace ShaderMotion {
public class MotionLayout {
	public struct ShapeIndex {
		public string shape;
		public int index;
		public float weight;
	}
	public readonly int[][] channels;
	public readonly int[]   baseIndices;
	public readonly List<ShapeIndex> shapeIndices = new List<ShapeIndex>();
	public MotionLayout(Skeleton skeleton, (int, HumanBodyBones[])[] humanLayout) {
		channels = new int[skeleton.bones.Length][];
		for(int i=0; i<skeleton.bones.Length; i++) {
			var chan = new List<int>();
			if(skeleton.bones[i] && skeleton.parents[i] < 0)
				chan.AddRange(Enumerable.Range(3, 12));
			else
				for(int j=0; j<3; j++)
					if(!float.IsNaN(skeleton.axes[i].max[j]))
						chan.Add(j);
			channels[i] = chan.ToArray();
		}
		baseIndices = Enumerable.Repeat(-1, skeleton.bones.Length).ToArray();
		foreach(var kv in humanLayout) {
			var slot = kv.Item1;
			foreach(var humanBone in kv.Item2) {
				baseIndices[(int)humanBone] = slot;
				slot += channels[(int)humanBone].Length;
			}
		}
		if(baseIndices.Any(i => i < 0))
			Debug.LogWarning("some bones are not bound to slot");
	}
	public static (int, HumanBodyBones[])[] defaultHumanLayout = new []{
		// roughly ordered by HumanTrait.GetBoneDefaultHierarchyMass
		(0, new []{
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
			// 77
		}),
		(90, new []{
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
			// 130
		}),
	};

	public void AddEncoderVisemeShapes(Mesh mesh=null, int baseIndex=80) {
		foreach(var vc in visemeTable)
			for(int i=0; i<3; i++) {
				var name = "v_"+vc.Item1;
				shapeIndices.Add(new ShapeIndex{shape=name, index=baseIndex+i, weight=vc.Item2[i]});
			}
	}
	public void AddDecoderVisemeShapes(Mesh mesh=null, int baseIndex=80) {
		var shapeNames = new List<string>();
		if(mesh)
			for(int i=0; i<mesh.blendShapeCount; i++)
				shapeNames.Add(mesh.GetBlendShapeName(i));

		foreach(var vc in visemeTable)
			for(int i=0; i<3; i++)
				if(vc.Item2[i] == 1) {
					var name = searchVisemeName(shapeNames, vc.Item1);
					shapeIndices.Add(new ShapeIndex{shape=name, index=baseIndex+i, weight=vc.Item2[i]});
				}
	}
	// express visemes as weighted sums of A/CH/O, widely used by CATS
	// https://github.com/GiveMeAllYourCats/cats-blender-plugin/blob/master/tools/viseme.py#L102
	static (string, Vector3)[] visemeTable = new (string, Vector3)[]{
		("sil", Vector3.zero),
		("PP", new Vector3(0.0f, 0.0f, 0.0f)),
		("FF", new Vector3(0.2f, 0.4f, 0.0f)),
		("TH", new Vector3(0.4f, 0.0f, 0.15f)),
		("DD", new Vector3(0.3f, 0.7f, 0.0f)),
		("kk", new Vector3(0.7f, 0.4f, 0.0f)),
		("CH", new Vector3(0.0f, 1.0f, 0.0f)),
		("SS", new Vector3(0.0f, 0.8f, 0.0f)),
		("nn", new Vector3(0.2f, 0.7f, 0.0f)),
		("RR", new Vector3(0.0f, 0.5f, 0.3f)),
		("aa", new Vector3(1.0f, 0.0f, 0.0f)),
		("E",  new Vector3(0.0f, 0.7f, 0.3f)),
		("ih", new Vector3(0.5f, 0.2f, 0.0f)),
		("oh", new Vector3(0.2f, 0.0f, 0.8f)),
		("ou", new Vector3(0.0f, 0.0f, 1.0f)),
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