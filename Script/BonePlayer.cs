using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using UnityEngine;
using Unity.Collections;
using AsyncGPUReadbackRequest = UnityEngine.Rendering.AsyncGPUReadbackRequest;

namespace ShaderMotion {
public class BonePlayer {
	Skeleton skeleton;
	MotionLayout layout;
	public SkinnedMeshRenderer shapeRenderer = null;

	// decodec motion
	public Quaternion rootQ;
	public Vector3 rootT;
	public float rootScale;
	public Vector3[] muscles;
	public Dictionary<string, float> shapes;

	public BonePlayer(Skeleton skeleton, MotionLayout layout) {
		this.skeleton = skeleton;
		this.layout = layout;

		muscles = new Vector3[skeleton.bones.Length];
		shapes = new Dictionary<string, float>();
	}

	static int width = 40, height = 45;
	NativeArray<Color> colors = new NativeArray<Color>();
	float SampleSlot(int idx) {
		int x = idx / height;
		int y = idx % height;
		return colors[x + (height-1-y) * width].r;
	}	
	public void Update(AsyncGPUReadbackRequest req) {
		colors = req.GetData<Color>();

		rootT = new Vector3(0,1,0);
		var rootY = Vector3.up;
		var rootZ = Vector3.forward;

		Array.Clear(muscles, 0, muscles.Length);
		for(int i=0; i<skeleton.bones.Length; i++) {
			var slot = layout.baseIndices[i];
			foreach(var j in layout.channels[i]) {
				var v = SampleSlot(slot);
				if(j<3)
					muscles[i][j] = v * 180;
				else if(j<6) {
					// TODO: high precision not implemented
				}
				else if(j<9)
					rootT[j-6] = v * 2;
				else if(j<12)
					rootY[j-9] = v * 2;
				else if(j<15)
					rootZ[j-12] = v * 2;

				slot++;
			}
		}
		rootQ = Quaternion.LookRotation(rootZ, rootY);
		rootScale = rootY.magnitude;

		shapes.Clear();
		foreach(var si in layout.shapeIndices) {
			float w = 0;
			shapes.TryGetValue(si.shape, out w);
			shapes[si.shape] = w + SampleSlot(si.index) * si.weight;
		}
	}
	void ApplyRootMotion() {
		var axes = skeleton.axes[(int)HumanBodyBones.Hips];
		var rescale = rootScale / skeleton.scale;
		skeleton.root.localScale = new Vector3(1,1,1) * rescale;
		skeleton.bones[(int)HumanBodyBones.Hips].SetPositionAndRotation(
						skeleton.root.TransformPoint(rootT / rescale),
						skeleton.root.rotation * rootQ * Quaternion.Inverse(axes.postQ));
	}
	public void ApplyTransform() {
		ApplyRootMotion();
		for(int i=0; i<HumanTrait.BoneCount; i++)
			if(skeleton.bones[i]) {
				var axes = skeleton.axes[i];
				if(i != (int)HumanBodyBones.Hips)
					skeleton.bones[i].localRotation = axes.preQ * muscleToRotation(axes.sign * muscles[i])
												* Quaternion.Inverse(axes.postQ);
			}
	}
	HumanPoseHandler poseHandler;
	HumanPose pose;
	public void ApplyHumanPose() {
		if(poseHandler == null) {
			poseHandler = new HumanPoseHandler(skeleton.root.GetComponent<Animator>().avatar, skeleton.root);
			poseHandler.GetHumanPose(ref pose);
		}
		pose.bodyPosition = rootT;
		pose.bodyRotation = rootQ;

		Array.Clear(pose.muscles, 0, pose.muscles.Length);
		for(int i=0; i<HumanTrait.BoneCount; i++)
			for(int j=0; j<3; j++) {
				var (muscle, weight) = boneMuscles[i, j];
				if(muscle >= 0)
					pose.muscles[muscle] += muscles[i][j] * weight;
			}
		for(int i=0; i<HumanTrait.MuscleCount; i++)
			pose.muscles[i] /= pose.muscles[i] >= 0 ? muscleLimits[i,1] : -muscleLimits[i,0];
		poseHandler.SetHumanPose(ref pose);

		ApplyRootMotion();
	}
	public void ApplyBlendShape() {
		if(shapeRenderer) {
			var mesh = shapeRenderer.sharedMesh;
			foreach(var kv in shapes) {
				var shape = mesh.GetBlendShapeIndex(kv.Key);
				var frame = mesh.GetBlendShapeFrameCount(shape)-1;
				var weight = mesh.GetBlendShapeFrameWeight(shape, frame);
				shapeRenderer.SetBlendShapeWeight(shape, kv.Value * weight);
			}
		}
	}
	static Quaternion muscleToRotation(Vector3 muscle) {
		var muscleYZ = new Vector3(0, muscle.y, muscle.z);
		return Quaternion.AngleAxis(muscleYZ.magnitude, muscleYZ.normalized)
				* Quaternion.AngleAxis(muscle.x, new Vector3(1,0,0));
	}

	static (int, float)[,] boneMuscles;
	static float[,] muscleLimits;
	static BonePlayer() {
		boneMuscles = new (int, float)[HumanTrait.BoneCount, 3];
		for(int i=0; i<HumanTrait.BoneCount; i++) 
			for(int j=0; j<3; j++) {
				var ii = i;
				var jj = j;
				var muscle = HumanTrait.MuscleFromBone(ii, jj);
				var weight = (float)1;
				if(muscle < 0) {
					switch(ii) {
					case (int)HumanBodyBones.LeftShoulder:
						ii = (int)HumanBodyBones.LeftUpperArm; break;
					case (int)HumanBodyBones.RightShoulder:
						ii = (int)HumanBodyBones.RightUpperArm; break;
					case (int)HumanBodyBones.Jaw:
						break;
					case (int)HumanBodyBones.LeftLowerArm:
					case (int)HumanBodyBones.RightLowerArm:
						weight = -1;
						jj = 0;
						goto default;
					case (int)HumanBodyBones.LeftLowerLeg:
					case (int)HumanBodyBones.RightLowerLeg:
						jj = 0;
						goto default;
					default:
						ii = HumanTrait.GetParentBone(ii);break;
					}
					muscle = HumanTrait.MuscleFromBone(ii, jj);
				}
				boneMuscles[i, j] = (muscle, weight);
			}
		muscleLimits = new float[HumanTrait.MuscleCount, 2];
		for(int i=0; i<HumanTrait.MuscleCount; i++) {
			muscleLimits[i, 0] = HumanTrait.GetMuscleDefaultMin(i);
			muscleLimits[i, 1] = HumanTrait.GetMuscleDefaultMax(i);
		}
	}
}
}