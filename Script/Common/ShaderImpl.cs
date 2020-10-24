using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShaderMotion {
public class ShaderImpl {
	public static float DecodeVideoFloat(float hi, float lo, int pow) {
		hi = hi * ((pow-1)/2) + (pow-1)/2;
		lo = lo * ((pow-1)/2) + (pow-1)/2;
		var x = Mathf.RoundToInt(lo);
		var y = Mathf.Min(lo-x, 0);
		var z = Mathf.Max(lo-x, 0);
		var r = Mathf.RoundToInt(hi);
		if((r & 1) != 0)
			(x, y, z) = (pow-1-x, -z, -y);
		if(x == 0)
			y += Mathf.Min(0, hi-r);
		if(x == pow-1)
			z += Mathf.Max(0, hi-r);
		x += r*pow;
		x -= (pow*pow-1)/2;
		y += 0.5f;
		z -= 0.5f;
		return ((y + z) / Mathf.Max(Mathf.Abs(y), Mathf.Abs(z)) * 0.5f + x) / ((pow-1)/2);
	}
	public static (Vector3, Vector3) orthogonalize(Vector3 u, Vector3 v) {
		var B = Vector3.Dot(u,v) * -2;
		var A = Vector3.Dot(u,u) + Vector3.Dot(v,v);
		A += Mathf.Sqrt(Mathf.Abs(A*A - B*B));
		var U = A*u+B*v; U *= Vector3.Dot(u,U)/Vector3.Dot(U,U);
		var V = A*v+B*u; V *= Vector3.Dot(v,V)/Vector3.Dot(V,V);
		return (U, V);
	}
}
}