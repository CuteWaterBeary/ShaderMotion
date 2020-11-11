Shader "Motion/MeshRecorder (Sprite)" {
Properties {
	[ToggleUI] _AutoHide ("AutoHide", Float) = 1
	_Layer ("Layer", Float) = 0
	// this shader uses the following properties to fallback to invisible sprite shader in VRChat
	[HideInInspector] _Color ("Tint", Color) = (0,0,0,0)
	[HideInInspector] _MainTex ("Sprite Texture", 2D) = "black" {}
}
SubShader {
	Tags { "Queue"="Overlay" "RenderType"="Overlay" "PreviewType"="Plane" }
	UsePass "Motion/MeshRecorder/"
}
}