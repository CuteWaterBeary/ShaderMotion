using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShaderMotion {
public class MotionPlayer : MonoBehaviour  {
	[SerializeField]
	public RenderTexture motionBuffer;
	public Animator animator;
	public SkinnedMeshRenderer shapeRenderer;
	public int layer;
	public bool applyScale = true;

	[Header("Advanced settings")]
	public bool applyHumanPose;
	public Vector2Int resolution = new Vector2Int(80,45);
	public Vector3Int tileSize = new Vector3Int(2,1,3);
	public int tileRadix = 3;
	
	[System.NonSerialized]
	private GPUReader gpuReader = new GPUReader();
	[System.NonSerialized]
	private Skeleton skeleton;
	[System.NonSerialized]
	private MotionDecoder decoder;
	
	void OnEnable() {
		if(!animator)
			animator = GetComponent<Animator>();
		if(!shapeRenderer)
			shapeRenderer = (animator?.GetComponentsInChildren<SkinnedMeshRenderer>() ?? new SkinnedMeshRenderer[0])
				.Where(smr => (smr.sharedMesh?.blendShapeCount??0) > 0).FirstOrDefault();

		skeleton = new Skeleton(animator);
		var morph = new Morph(animator);
		var layout = new MotionLayout(skeleton, morph);
		decoder = new MotionDecoder(skeleton, morph, layout, resolution.x, resolution.y,
			tileWidth:tileSize.x, tileHeight:tileSize.y, tileDepth:tileSize.z, tileRadix:tileRadix);
	}
	void OnDisable() {
		skeleton = null;
		decoder = null;
	}
	void Update() {
		var request = gpuReader.Request(motionBuffer);
		if(request != null && !request.Value.hasError) {
			decoder.Update(request.Value, layer);
			if(applyHumanPose)
				ApplyHumanPose();
			else
				ApplyTransform();
			ApplyBlendShape();
		}
	}

	const float shapeWeightEps = 0.1f;
	private HumanPoseHandler poseHandler;
	private HumanPose humanPose;
	private Vector3[] swingTwists;
	void ApplyScale() {
		if(applyScale)
			skeleton.root.localScale = Vector3.one * (decoder.motions[0].s/skeleton.humanScale);
	}
	void ApplyHumanPose() {
		if(poseHandler == null) {
			poseHandler = new HumanPoseHandler(skeleton.root.GetComponent<Animator>().avatar, skeleton.root);
			poseHandler.GetHumanPose(ref humanPose);
		}
		ApplyScale();
		var motions = decoder.motions;
		System.Array.Resize(ref swingTwists, HumanTrait.BoneCount);
		for(int i=0; i<HumanTrait.BoneCount; i++)
			swingTwists[i] = motions[i].t;
		HumanPoser.SetBoneSwingTwists(ref humanPose, swingTwists);
		HumanPoser.SetHipsPositionRotation(ref humanPose, motions[0].t, motions[0].q, motions[0].s);
		poseHandler.SetHumanPose(ref humanPose);
		
	}
	void ApplyTransform() {
		ApplyScale();
		skeleton.bones[0].position = skeleton.root.TransformPoint(
			decoder.motions[0].t / (decoder.motions[0].s/skeleton.humanScale));
		for(int i=0; i<skeleton.bones.Length; i++)
			if(skeleton.bones[i]) {
				var axes = skeleton.axes[i];
				if(!skeleton.dummy[i])
					skeleton.bones[i].localRotation = axes.preQ * decoder.motions[i].q * Quaternion.Inverse(axes.postQ);
				else // TODO: this assumes non-dummy precedes dummy bone and breaks for missing Neck
					skeleton.bones[i].localRotation *= axes.postQ * decoder.motions[i].q * Quaternion.Inverse(axes.postQ);
			}
	}
	void ApplyBlendShape() {
		if(shapeRenderer) {
			var mesh = shapeRenderer.sharedMesh;
			foreach(var kv in decoder.shapes) {
				var idx = mesh.GetBlendShapeIndex(kv.Key);
				if(idx >= 0)
					shapeRenderer.SetBlendShapeWeight(idx,
						Mathf.Round(Mathf.Clamp01(kv.Value)*100/shapeWeightEps)*shapeWeightEps);
			}
		}
	}
}
}