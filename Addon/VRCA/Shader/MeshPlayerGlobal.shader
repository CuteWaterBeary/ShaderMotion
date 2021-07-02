Shader "Motion/MeshPlayerGlobal" {
Properties {
	[Header(Texture)]
	[NoScaleOffset]
	_MainTex ("MainTex", 2D) = "white" {}
	_Color ("Color", Color) = (1,1,1,1)

	[Header(Culling)]
	[Enum(UnityEngine.Rendering.CullMode)] _Cull("Face Culling", Float) = 2
	[Toggle(_ALPHATEST_ON)] _AlphaTest("Alpha Test", Float) = 0
	_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0

	[HideInInspector] _Bone ("Bone", 2D) = "black" {}
	[HideInInspector] _Shape ("Shape", 2D) = "black" {}
	[Header(Motion)]
	_HumanScale ("HumanScale (hips height: 0=original, -1=encoded)", Float) = -1
	_Layer ("Layer (location of motion stripe)", Float) = 0
	_RotationTolerance ("RotationTolerance", Range(0, 1)) = 0.1
}
SubShader {
	Tags { "Queue"="Geometry" "RenderType"="Opaque" }
	UsePass "Motion/MeshPlayer/"
}
}