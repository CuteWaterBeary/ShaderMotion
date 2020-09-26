using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;

namespace ShaderMotion {
public class Util {
	public static Quaternion fromSwingTwist(Vector3 degree) {
		var degreeYZ = new Vector3(0, degree.y, degree.z);
		return Quaternion.AngleAxis(degreeYZ.magnitude, degreeYZ.normalized)
				* Quaternion.AngleAxis(degree.x, new Vector3(1,0,0));
	}
	public static float DecodeVideoFloat(float hi, float lo) {
		const int range = 3*3*3*3*3*3;
		hi = hi * ((range-1)/2) + (range-1)/2;
		lo = lo * ((range-1)/2) + (range-1)/2;
		var x = Mathf.RoundToInt(lo);
		var y = Mathf.Min(lo-x, 0);
		var z = Mathf.Max(lo-x, 0);
		var r = Mathf.RoundToInt(hi);
		if((r & 1) != 0)
			(x, y, z) = (range-1-x, -z, -y);
		if(x == 0)
			y += Mathf.Min(0, hi-r);
		if(x == range-1)
			z += Mathf.Max(0, hi-r);
		x += r*range;
		x -= (range*range-1)/2;
		y += 0.5f;
		z -= 0.5f;
		return ((y + z) / Mathf.Max(Mathf.Abs(y), Mathf.Abs(z)) * 0.5f + x) / ((range-1)/2);
	}
	public static (Vector3, Vector3) conformalize(Vector3 u, Vector3 v) {
		var c = new Vector3(Vector3.Dot(u,u), Vector3.Dot(v,v), Vector3.Dot(u,v))
				/ Mathf.Sqrt(Mathf.Max(1e-9f, Vector3.Dot(u,u)*Vector3.Dot(v,v) - Vector3.Dot(u,v)*Vector3.Dot(u,v)));
		return ((u + c.y*u - c.z*v) / 2, (v + c.x*v - c.z*u) / 2);
	}
}
}