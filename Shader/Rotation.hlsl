static float PI = 3.14159265;
#define c0 _11_21_31
#define c1 _12_22_32
#define c2 _13_23_33
#define c3 _14_24_34

float3x3 expAxisAngle(float3 axisAngle, float3x3 v, float eps=1e-5) {
	float angle = length(axisAngle), co = cos(angle), si = sin(angle);
	float3 si_axis = axisAngle * (angle < eps ? 1 : si/angle);
	float3 rc_axis = axisAngle * (angle < eps ? rsqrt(2) : sqrt(1-co)/angle);
	v.c0 = co * v.c0 + (dot(rc_axis, v.c0) * rc_axis + cross(si_axis, v.c0)); // MAD optimization
	v.c1 = co * v.c1 + (dot(rc_axis, v.c1) * rc_axis + cross(si_axis, v.c1));
	v.c2 = co * v.c2 + (dot(rc_axis, v.c2) * rc_axis + cross(si_axis, v.c2));
	return v;
}
// (x,y,z) => exp(yJ+zK) * exp(xI)
float3x3 muscleToRotation(float3 muscle) {
	return expAxisAngle(float3(0, muscle.yz), float3x3(
		1, 0, 0,
		0, cos(muscle.x), -sin(muscle.x),
		0, +sin(muscle.x), cos(muscle.x)));
}
float3 rotationToMuscle(float3x3 rot, float eps=1e-5) {
	float cosYZ = rot.c0.x;
	// NOTE: degeneracy cosYZ == -1 is omitted due to its non-unique solution & rarity (bone is flipped)
	return float3(atan2(rot[2][1]-rot[1][2], rot[1][1]+rot[2][2]),
		 	float2(-rot.c0.z, rot.c0.y) * (cosYZ > 1-eps ? 4.0/3 - cosYZ/3 : acos(cosYZ) * rsqrt(1-cosYZ*cosYZ)));
}
float3x3 mulEulerYXZ(float3x3 m, float3 rad) {
	float3x3 m0;
	float3 co = cos(rad), si = sin(rad);
	m0 = m;
	m.c2 = m0.c2*co.y + m0.c0*si.y;
	m.c0 = m0.c0*co.y - m0.c2*si.y;
	m0 = m;
	m.c1 = m0.c1*co.x + m0.c2*si.x;
	m.c2 = m0.c2*co.x - m0.c1*si.x;
	m0 = m;
	m.c0 = m0.c0*co.z + m0.c1*si.z;
	m.c1 = m0.c1*co.z - m0.c0*si.z;
	return m;
}
