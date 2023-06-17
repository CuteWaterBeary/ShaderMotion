using UnityEngine;

namespace ShaderMotion {
[RequireComponent(typeof(Camera))]
#if !UDON
public class CamRTReader : MonoBehaviour
#else
[UdonSharp.UdonBehaviourSyncMode(UdonSharp.BehaviourSyncMode.None)]
public class CamRTReader : UdonSharp.UdonSharpBehaviour
#endif
{
	public Texture2D outputTexture;

	private RenderTexture inputTexture;
	private Color[] colors;
	void OnEnable() {
		inputTexture = GetComponent<Camera>().targetTexture;
		colors = new Color[inputTexture.width*inputTexture.height];
	}
	void OnPostRender() {
#if !UDON
		UnityEngine.Rendering.AsyncGPUReadback.Request(inputTexture, 0, TextureFormat.RGBAFloat, OnAsyncGpuReadbackComplete);
#elif UNITY_ANDROID // TODO: readback doesn't work yet
		outputTexture.ReadPixels(new Rect(0, 0, inputTexture.width, inputTexture.height), 0, 0, false);
#else
		VRC.SDK3.Rendering.VRCAsyncGPUReadback.Request(inputTexture, 0, TextureFormat.RGBAFloat, (VRC.Udon.Common.Interfaces.IUdonEventReceiver)(Component)this);
#endif
	}

#if !UDON
	public void OnAsyncGpuReadbackComplete(UnityEngine.Rendering.AsyncGPUReadbackRequest request)
#else
	public void OnAsyncGpuReadbackComplete(VRC.SDK3.Rendering.VRCAsyncGPUReadbackRequest request)
#endif
	{
		if (request.hasError || !request.done) return;
#if !UDON
		request.GetData<Color>().CopyTo(colors);
#else
		request.TryGetData(colors);
#endif
		outputTexture.SetPixels(colors);
	}
}
}