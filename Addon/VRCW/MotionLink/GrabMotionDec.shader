Shader "Motion/GrabMotionDec" {
Properties {
	[HideInInspector] _MainTex ("MainTex", 2D) = "white" {}
}
SubShader {
	Tags { "Queue"="Geometry" "RenderType"="Opaque" }
	Pass {
		Tags { "LightMode"="Vertex" }
		ColorMask 0
		ZTest Off
	}
	GrabPass {
		Tags { "LightMode"="Vertex" }
		"_MotionDec"
	}
}
}