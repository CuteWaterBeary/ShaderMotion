static bool IsOrtho = unity_OrthoParams.w;
#if defined(USING_STEREO_MATRICES)
	static bool IsStereo = true;
#else
	static bool IsStereo = false;
#endif
static bool IsTilted = abs(UNITY_MATRIX_V[0].y) > 1e-5;
static bool IsInMirror = determinant((float3x3)UNITY_MATRIX_V) > 0;
static bool IsInPhoto = all(_ScreenParams.xy == float2(1920, 1080));
