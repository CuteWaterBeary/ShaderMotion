Shader "Motion/MeshRecorder (Sprite)" {
Properties {
	[ToggleUI] _AutoHide ("AutoHide", Float) = 1
	_Layer ("Layer", Float) = 0
	// this shader is used to fallback to invisible sprite shader in VRChat
	[HideInInspector] _Color("Color", Color) = (0,0,0,0)
}
SubShader {
	Tags { "Queue"="Overlay" "RenderType"="Overlay" "PreviewType"="Plane" }
	UsePass "Motion/MeshRecorder/"
}
}