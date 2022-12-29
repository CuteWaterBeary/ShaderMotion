using UnityEngine;

namespace ShaderMotion {
[RequireComponent(typeof(Camera))]
#if UDON
[UdonSharp.UdonBehaviourSyncMode(UdonSharp.BehaviourSyncMode.None)]
public class CamRTReader : UdonSharp.UdonSharpBehaviour
#else
public class CamRTReader : MonoBehaviour
#endif
{
	public Texture2D outputTexture;

	private RenderTexture inputTexture;
	void OnEnable() {
		inputTexture = GetComponent<Camera>().targetTexture;
	}
	void OnPostRender() {
		outputTexture.ReadPixels(new Rect(0, 0, inputTexture.width, inputTexture.height), 0, 0, false);
	}
}
}