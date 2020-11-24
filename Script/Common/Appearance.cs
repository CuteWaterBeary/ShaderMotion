using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;

namespace ShaderMotion {
public class Appearance {
	public Dictionary<string, float>[] exprShapes;
	// public List<string> shapes;
	
	public Appearance(Mesh mesh, bool primary) {
		var shapeNames = new List<string>(Enumerable.Range(0, mesh?.blendShapeCount??0)
											.Select(i => mesh.GetBlendShapeName(i)));

		exprShapes = Enumerable.Range(0, 3).Select(_ => new Dictionary<string, float>()).ToArray();
		// shapes = new List<string>();
		foreach(var viseme in visemeTable) {
			// var shape = shapes.Count;
			var name = searchVisemeName(shapeNames, viseme.name);
			// shapes.Add(name);
			for(int i=0; i<3; i++)
				if(viseme.weights[i] > 0 && (!primary || viseme.weights[i] == 1))
					exprShapes[i][name] = viseme.weights[i];
		}
	}
	// express visemes as weighted sums of A/CH/O, widely used by CATS
	// https://github.com/GiveMeAllYourCats/cats-blender-plugin/blob/master/tools/viseme.py#L102
	static (string name, Vector3 weights)[] visemeTable = new (string, Vector3)[]{
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