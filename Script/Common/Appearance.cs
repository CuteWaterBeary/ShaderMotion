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

		exprShapes = Enumerable.Range(0, 5).Select(_ => new Dictionary<string, float>()).ToArray();
		// shapes = new List<string>();
		foreach(var viseme in visemeTable) {
			// var shape = shapes.Count;
			var name = searchVisemeName(shapeNames, viseme.name);
			// shapes.Add(name);
			for(int i=0; i<5; i++)
				if(viseme.weights[i] > 0 && (!primary || viseme.weights[i] == 1)) {
					exprShapes[i][name] = viseme.weights[i];
					// Debug.Log($"exprShapes[{i}][{name}]= {viseme.weights[i]}");
				}
		}
	}
	// // express visemes as weighted sums of A/CH/O, widely used by CATS
	// // https://github.com/GiveMeAllYourCats/cats-blender-plugin/blob/master/tools/viseme.py#L102
	string searchVisemeName(IEnumerable<string> names, string viseme) {
		var r = new Regex($@"^({viseme})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		foreach(var name in names)
			if(r.IsMatch(name))
				return name;
		r = new Regex($@"[^A-Za-z0-9]({viseme})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		foreach(var name in names)
			if(r.IsMatch(name))
				return name;
		return $"v_{viseme.Split('|')[0]}";
	}
	static (string name, float[] weights)[] visemeTable = new (string,float[])[]{
		("pp",		new[]{  0f,   0f,   0f, 0f, 0f}),
		("ff",		new[]{0.2f, 0.4f,   0f, 0f, 0f}),
		("th",		new[]{0.4f,   0f, 0.1f, 0f, 0f}),
		("dd",		new[]{0.3f, 0.7f,   0f, 0f, 0f}),
		("kk",		new[]{0.7f, 0.4f,   0f, 0f, 0f}),
		("ch",		new[]{  0f, 1.0f,   0f, 0f, 0f}),
		("ss",		new[]{  0f, 0.8f,   0f, 0f, 0f}),
		("nn",		new[]{0.2f, 0.7f,   0f, 0f, 0f}),
		("rr",		new[]{  0f, 0.5f, 0.3f, 0f, 0f}),
		("aa|a|ah",	new[]{  1f,   0f,   0f, 0f, 0f}),
		("e|ee|eh",	new[]{  0f,   0f,   0f, 1f, 0f}),
		("ih|i",	new[]{  0f,   1f,   0f, 0f, 0f}),
		("oh|o",	new[]{  0f,   0f,   0f, 0f, 1f}),
		("ou|u",	new[]{  0f,   0f,   1f, 0f, 0f}),
	};
}
}