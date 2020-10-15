using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;

namespace ShaderMotion {
public class MotionPlayer : MonoBehaviour  {
	[SerializeField]
	public RenderTexture motionBuffer;
	public Animator animator;
	public SkinnedMeshRenderer shapeRenderer;
	public int layer;
	public bool applyHumanPose; // for testing
	
	[System.NonSerialized]
	private GPUReader gpuReader = new GPUReader();
	[System.NonSerialized]
	private BonePlayer player;
	
	void OnEnable() {
		// unbox null
		var animator = (this.animator?this.animator:null)??GetComponent<Animator>(); 
		var shapeRenderer = this.shapeRenderer?this.shapeRenderer:null;

		var skeleton = new Skeleton(animator);
		var layout = new MotionLayout(skeleton, MotionLayout.defaultHumanLayout);
		layout.AddDecoderVisemeShapes(shapeRenderer?.sharedMesh);
		player = new BonePlayer(skeleton, layout);
		player.shapeRenderer = shapeRenderer;
		OnValidate();
	}
	void OnDisable() {
		player = null;
	}
	void OnValidate() {
		// allow to change parameter on the fly
		if(player != null)
			player.layer = layer;
    }

	void Update() {
		var request = gpuReader.Request(motionBuffer);
		if(request != null && !request.Value.hasError) {
			player.Update(request.Value);
			if(applyHumanPose)
				player.ApplyHumanPose();
			else
				player.ApplyTransform();
			player.ApplyBlendShape();
		}
	}
}
}