using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;

namespace ShaderMotion {
public class MotionPlayer : MonoBehaviour  {
	public RenderTexture motionBuffer;
	public Animator animator;
	public SkinnedMeshRenderer shapeRenderer;
	public bool applyHumanPose = false;
	
	GPUReader gpuReader = new GPUReader();
	BonePlayer player;

	void OnEnable() {
		// unbox null
		var animator = this.animator?this.animator:null; 
		var shapeRenderer = this.shapeRenderer?this.shapeRenderer:null;

		var armature = new HumanUtil.Armature(animator??GetComponent<Animator>(), FrameLayout.defaultHumanBones);
		var layout = new FrameLayout(armature, FrameLayout.defaultOverrides);
		layout.AddDecoderVisemeShapes(shapeRenderer?.sharedMesh);
		player = new BonePlayer(armature, layout);
		player.shapeRenderer = shapeRenderer;
	}
	void OnDisable() {
		player = null;
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