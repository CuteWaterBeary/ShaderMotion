using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using AsyncGPUReadback = UnityEngine.Rendering.AsyncGPUReadback;
using AsyncGPUReadbackRequest = UnityEngine.Rendering.AsyncGPUReadbackRequest;

namespace ShaderMotion {
public class GPUReader {
	Queue<AsyncGPUReadbackRequest> requests = new Queue<AsyncGPUReadbackRequest>();
	public AsyncGPUReadbackRequest? Request(Texture tex) {
		AsyncGPUReadbackRequest? request = null;
		while(requests.Count > 0) {
			var r = requests.Peek();
			if(!r.done)
				break;
			request = requests.Dequeue();
		}
		if(requests.Count < 2)
			requests.Enqueue(AsyncGPUReadback.Request(tex));
		return request;
	}
}
public class AnimatorPlayer : MonoBehaviour  {
	public RenderTexture motionBuffer;
	public Animator animator = null;
	public SkinnedMeshRenderer shapeRenderer = null;
	public bool applyHumanPose = false;
	
	GPUReader gpuReader = new GPUReader();
	MotionPlayer player;

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
	void Start() {
		if(animator == null)
			animator = GetComponent<Animator>();
		if(shapeRenderer == null)
			shapeRenderer = null;
		var armature = new HumanUtil.Armature(animator, FrameLayout.defaultHumanBones);
		var layout = new FrameLayout(armature, FrameLayout.defaultOverrides);
		layout.AddDecoderVisemeShapes(shapeRenderer?.sharedMesh);
		player = new MotionPlayer(armature, layout);
		player.shapeRenderer = shapeRenderer;
	}
}
}