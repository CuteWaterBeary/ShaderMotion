using UnityEngine;

namespace ShaderMotion {
#if UDON
[UdonSharp.UdonBehaviourSyncMode(UdonSharp.BehaviourSyncMode.None)]
public class UdonPlayer : UdonSharp.UdonSharpBehaviour
#else
public class UdonPlayer : MonoBehaviour
#endif
{
	public Texture2D dataTexture;
	public Animator animator;
	public int layer;
	public bool applyScale = true;
	public bool applyVisibility = true;

	private Transform root;
	private Transform[] bones;
	private Renderer[] renderers;
	void OnEnable() {
		if(!animator)
			animator = GetComponent<Animator>();
		root = animator.transform;
		renderers = GetComponentsInChildren<Renderer>(false);
		bones = new Transform[HumanTrait.BoneCount];
		for(int i=0; i<bones.Length; i++)
			bones[i] = animator.GetBoneTransform((HumanBodyBones)i);
		bones[(int)HumanBodyBones.UpperChest] = null; // TODO: hierarchy order
	}
	void Update() {
		// TODO: blendshape
		ApplyTransform();
	}
	void ApplyTransform() {
		var offsetY = layer*4;
		var c1 = (Vector3)(Vector4)dataTexture.GetPixel(0, offsetY+1);
		var c3 = (Vector3)(Vector4)dataTexture.GetPixel(0, offsetY+3);
		var rescale = c1.magnitude;
		var valid = !float.IsNaN(c3.magnitude) && rescale > 0f;
		if(applyVisibility)
			foreach(var renderer in renderers)
				renderer.enabled = valid;
		if(!valid)
			return;
		if(applyScale)
			root.localScale = Vector3.one*rescale;
		bones[0].position = root.TransformPoint(c3/rescale);
		ApplyRotation(offsetY);
	}
	void ApplyRotation(int offsetY) {
		var preQ = root.rotation;
		var scale = root.lossyScale;
		var postQ = Quaternion.LookRotation(scale.x*scale.y*Vector3.forward, scale.x*scale.z*Vector3.up); // det(sign)*scale(sign)
		preQ *= postQ;
		for(int n=bones.Length, i=0; i<n; i++) {
			var bone = bones[i];
			if(bone) {
				var c1 = (Vector3)(Vector4)dataTexture.GetPixel(i, offsetY+1);
				var c2 = (Vector3)(Vector4)dataTexture.GetPixel(i, offsetY+2);
				bone.rotation = preQ * Quaternion.LookRotation(c2, c1) * postQ; // conjugate postQ to handle mirror
			}
		}
	}
}
}