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
	public bool applyHumanPose; // for testing
	
	[System.NonSerialized]
	private GPUReader gpuReader = new GPUReader();
	[System.NonSerialized]
	private Skeleton skeleton;
	[System.NonSerialized]
	private MotionDecoder decoder;
	
	void OnEnable() {
		var animator = this.animator ? this.animator : GetComponent<Animator>(); 
		var shapeRenderer = this.shapeRenderer ? this.shapeRenderer : null;

		skeleton = new Skeleton(animator);
		var layout = new MotionLayout(skeleton, MotionLayout.defaultHumanLayout);
		layout.AddDecoderVisemeShapes(shapeRenderer?.sharedMesh);
		decoder = new MotionDecoder(skeleton, layout);
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

	private HumanPoseHandler poseHandler;
	private HumanPose humanPose;
	private Vector3[] swingTwists;
	void ApplyScale() {
		skeleton.root.localScale = decoder.motions[0].s / skeleton.scale * new Vector3(1,1,1);
	}
	void ApplyHumanPose() {
		if(poseHandler == null) {
			poseHandler = new HumanPoseHandler(skeleton.root.GetComponent<Animator>().avatar, skeleton.root);
			poseHandler.GetHumanPose(ref humanPose);
		}
		var motions = decoder.motions;
		System.Array.Resize(ref swingTwists, HumanTrait.BoneCount);
		for(int i=0; i<HumanTrait.BoneCount; i++)
			swingTwists[i] = motions[i].t;
		HumanPoser.SetBoneSwingTwists(ref humanPose, swingTwists);
		HumanPoser.SetHipsPositionRotation(ref humanPose, motions[0].t / motions[0].s, motions[0].q);
		poseHandler.SetHumanPose(ref humanPose);
		ApplyScale();
	}
	void ApplyTransform() {
		ApplyScale();
		skeleton.bones[0].position = skeleton.root.TransformPoint(decoder.motions[0].t / skeleton.root.localScale.y);
		for(int i=0; i<skeleton.bones.Length; i++)
			if(skeleton.bones[i]) {
				var axes = skeleton.axes[i];
				if(!skeleton.dummy[i])
					skeleton.bones[i].localRotation = axes.preQ * decoder.motions[i].q * Quaternion.Inverse(axes.postQ);
				else // TODO: this assumes non-dummy precedes dummy bone, so it fails on Neck
					skeleton.bones[i].localRotation *= axes.postQ * decoder.motions[i].q * Quaternion.Inverse(axes.postQ);
			}
	}
	void ApplyBlendShape() {
		if(shapeRenderer) {
			var mesh = shapeRenderer.sharedMesh;
			foreach(var kv in decoder.shapes) {
				var shape = mesh.GetBlendShapeIndex(kv.Key);
				var frame = mesh.GetBlendShapeFrameCount(shape)-1;
				var weight = mesh.GetBlendShapeFrameWeight(shape, frame);
				shapeRenderer.SetBlendShapeWeight(shape, kv.Value * weight);
			}
		}
	}
}
}