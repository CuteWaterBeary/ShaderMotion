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
	float3x3 m = expAxisAngle(float3(0, muscle.yz), float3x3(
		1, 0, 0,
		0, cos(muscle.x), -sin(muscle.x),
		0, +sin(muscle.x), cos(muscle.x)));
	m.c2 = cross(m.c0, m.c1); // save instruction
	return m;
}
float3 rotationToMuscle(float3x3 rot, float eps=1e-5) {
	float cosYZ = rot.c0.x;
	// NOTE: degeneracy cosYZ == -1 is omitted due to its non-unique solution & rarity (bone is flipped)
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
void orthonormalize(float3 u, float3 v, out float3 U, out float3 V) {
	float rsq = rsqrt(abs(dot(u,u)*dot(v,v)-dot(u,v)*dot(u,v)));
	U = normalize(u+rsq*(dot(v,v)*u-dot(u,v)*v));
	V = normalize(v+rsq*(dot(u,u)*v-dot(u,v)*u));
}