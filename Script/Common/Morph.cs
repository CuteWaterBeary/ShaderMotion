using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShaderMotion {
public class BlendSpace {
	public string name;
	public KeyValuePair<string,float>[,][] shapes = new KeyValuePair<string,float>[3,3][];
	static float GetWeight(Vector2 coord, int i, int j) {
		// TODO: try different interpolation?
		return Mathf.Clamp01(1-Mathf.Abs(coord.x-(i-1))) * Mathf.Clamp01(1-Mathf.Abs(coord.y-(j-1)));
	}
	public void Sample(Vector2 coord, IDictionary<string,float> weights, bool writeDefault=false) {
		for(int i=0; i<3; i++)
		for(int j=0; j<3; j++)
			if(shapes[i,j] != null) {
				var w = GetWeight(coord, i, j);
				if(writeDefault || w != 0)
					foreach(var shape in shapes[i,j]) {
						float v;
						weights.TryGetValue(shape.Key, out v);
						weights[shape.Key] = v + shape.Value * w;
					}
			}
	}
	public void Set(Vector2 coord, KeyValuePair<string,float>[] shapes) {
		var i = 1+(int)System.Math.Sign(coord.x);
		var j = 1+(int)System.Math.Sign(coord.y);
		this.shapes[i,j] = null;
		if(shapes != null) {
			var w = GetWeight(coord, i, j);
			var weights = shapes.ToDictionary(x => x.Key, x => -x.Value);
			Sample(coord, weights);
			this.shapes[i,j] = weights.Select(x => new KeyValuePair<string,float>(x.Key, -x.Value/w)).ToArray();
		}
	}
}
public class Morph {
	public BlendSpace[] blends = new BlendSpace[(int)BlendSpacePreset.LastPreset];
	public Dictionary<string, (int,Vector2)> controls = new Dictionary<string, (int,Vector2)>();
	public Morph(Animator animator) {
		MorphSettings.Apply(this, animator);
	}
	public static void GetBlendShapeVertices(Mesh mesh, IEnumerable<KeyValuePair<string,float>> shapes, Vector3[] dstDV, Vector3[] srcDV) {
		if(shapes != null)
			foreach(var shape in shapes) {
				var idx = mesh.GetBlendShapeIndex(shape.Key);
				if(idx >= 0) {
					mesh.GetBlendShapeFrameVertices(idx, mesh.GetBlendShapeFrameCount(idx)-1, srcDV, null, null);
					for(int v=0; v<srcDV.Length; v++)
						dstDV[v] += srcDV[v] * shape.Value;
				}
			}
	}
}
public enum BlendSpacePreset {
	LipSync,
	Blink,
	Emotion,
	LastPreset,
}
}