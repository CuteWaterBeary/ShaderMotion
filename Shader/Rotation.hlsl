#define c0 _11_21_31
#define c1 _12_22_32
#define c2 _13_23_33
#define c3 _14_24_34

float3x3 fromToRotate(float3 src, float3 dst, float3x3 v) {
	float co = dot(src, dst);
	float3 si_axis = cross(src, dst);
	float3 rc_axis = rsqrt(1+co) * si_axis;
	v.c0 = co * v.c0 + (dot(rc_axis, v.c0) * rc_axis + cross(si_axis, v.c0)); // MAD optimization
	v.c1 = co * v.c1 + (dot(rc_axis, v.c1) * rc_axis + cross(si_axis, v.c1));
	v.c2 = co * v.c2 + (dot(rc_axis, v.c2) * rc_axis + cross(si_axis, v.c2));
	return v;
}
float3x3 axisAngleRotate(float3 axisAngle, float3x3 v, float eps=1e-5) {
	float angle = length(axisAngle), co = cos(angle), si = sin(angle);
	float3 si_axis = axisAngle * (angle < eps ? 1 : si/angle);
	float3 rc_axis = axisAngle * (angle < eps ? rsqrt(2) : sqrt(1-co)/angle);
	v.c0 = co * v.c0 + (dot(rc_axis, v.c0) * rc_axis + cross(si_axis, v.c0)); // MAD optimization
	v.c1 = co * v.c1 + (dot(rc_axis, v.c1) * rc_axis + cross(si_axis, v.c1));
	v.c2 = co * v.c2 + (dot(rc_axis, v.c2) * rc_axis + cross(si_axis, v.c2));
	return v;
}
float3x3 fromSwingTwist(float3 angles) {
	// (x,y,z) => exp(yJ+zK) * exp(xI)
	float3x3 m = axisAngleRotate(float3(0, angles.yz), float3x3(
		1, 0, 0,
		0, cos(angles.x), -sin(angles.x),
		0, +sin(angles.x), cos(angles.x)));
	m.c2 = cross(m.c0, m.c1); // save instruction
	return m;
}
float3 toSwingTwist(float3x3 rot, float eps=1e-5) {
	float cosYZ = rot.c0.x; // NOTE: degeneracy cosYZ == -1 isn't handled for its unlikeliness (bone is flipped)
	return float3(atan2(rot[2][1]-rot[1][2], rot[1][1]+rot[2][2]),
		 	float2(-rot.c0.z, rot.c0.y) * (cosYZ > 1-eps ? 4.0/3 - cosYZ/3 : acos(cosYZ) * rsqrt(1-cosYZ*cosYZ)));
}
float3x3 mulEulerYXZ(float3x3 m, float3 rad) {
	float3 co = cos(rad), si = sin(rad);
	m = mul(m, float3x3(co.y,0,+si.y, 0,1,0, -si.y,0,co.y));
	m = mul(m, float3x3(1,0,0, 0,co.x,-si.x, 0,+si.x,co.x));
	m = mul(m, float3x3(co.z,-si.z,0, +si.z,co.z,0, 0,0,1));
	return m;
}
// return a orthogonal pair (U,V) closest to (u,v)
float orthogonalize(float3 u, float3 v, out float3 U, out float3 V) {
	float B = dot(u,v) * -2;
	float A = dot(u,u) + dot(v,v);
	A += sqrt(abs(A*A - B*B));
	U = A*u+B*v, U *= dot(u,U)/dot(U,U);
	V = A*v+B*u, V *= dot(v,V)/dot(V,V);
	return dot(u-U, u-U) + dot(v-V, v-V);
}