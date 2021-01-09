#define c0 _11_21_31
#define c1 _12_22_32
#define c2 _13_23_33
#define c3 _14_24_34

float3x3 axisAngleRotate(float3 axisAngle, float3x3 v, float eps=1e-5) {
	float angle = length(axisAngle), co = cos(angle), si = sin(angle);
	float3 si_axis = axisAngle * (angle > eps ? si/angle : 1);
	float3 rc_axis = axisAngle * (angle > eps ? sqrt(1-co)/angle : rsqrt(2));
	v.c0 = co * v.c0 + (dot(rc_axis, v.c0) * rc_axis + cross(si_axis, v.c0)); // MAD optimization
	v.c1 = co * v.c1 + (dot(rc_axis, v.c1) * rc_axis + cross(si_axis, v.c1));
	v.c2 = co * v.c2 + (dot(rc_axis, v.c2) * rc_axis + cross(si_axis, v.c2));
	return v;
}
float3x3 swingTwistRotate(float3 angles) {
	float3x3 m = axisAngleRotate(float3(0, angles.yz), float3x3(
		1, 0, 0,
		0, cos(angles.x), -sin(angles.x),
		0, +sin(angles.x), cos(angles.x)));
	m.c2 = cross(m.c0, m.c1); // fewer instructions
	return m;
}
float3 swingTwistAngles(float3x3 rot, float eps=1e-5) {
	// NOTE: doesn't handle the singularity rot.c0 == (-1, 0, 0)
	return float3(atan2(rot.c1.z-rot.c2.y, rot.c1.y+rot.c2.z), float2(-rot.c0.z, rot.c0.y)
		* (rot.c0.x < 1-eps ? acos(rot.c0.x) * rsqrt(1-rot.c0.x*rot.c0.x) : 4./3 - rot.c0.x/3));
}
// find an orthogonal pair (U,V) closest to (u,v)
float orthogonalize(float3 u, float3 v, out float3 U, out float3 V) {
	float B = dot(u,v) * -2;
	float A = dot(u,u) + dot(v,v);
	A += sqrt(abs(A*A - B*B));
	U = A*u+B*v, U *= dot(u,U)/dot(U,U);
	V = A*v+B*u, V *= dot(v,V)/dot(V,V);
	return dot(u-U, u-U) + dot(v-V, v-V);
}