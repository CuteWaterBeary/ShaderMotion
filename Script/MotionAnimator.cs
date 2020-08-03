#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using AsyncGPUReadback = UnityEngine.Rendering.AsyncGPUReadback;
using AsyncGPUReadbackRequest = UnityEngine.Rendering.AsyncGPUReadbackRequest;

namespace ShaderMotion {
public class MotionAnimator : MonoBehaviour  {
	public RenderTexture motionBuffer;
	public Animator animator;
	public float frameRate = 30;
	public bool useRawMuscle = false;
	
	float dt = 0;
	Queue<AsyncGPUReadbackRequest> requests = new Queue<AsyncGPUReadbackRequest>();
	NativeArray<Color> colors = new NativeArray<Color>();

	void Update() {
		while(requests.Count > 0) {
			var request = requests.Peek();
			if(request.hasError)
				Debug.LogWarning("AsyncGPUReadbackRequest Error");
			else if(request.done) {
				colors = request.GetData<Color>();
				if(useRawMuscle)
					AnimateUsingHumanPose();
				else
					AnimateUsingRotation();
			} else
				break;
			requests.Dequeue();
		}
		dt += Time.deltaTime;
		if(dt*frameRate >= 1) {
			dt = 0;
			requests.Enqueue(AsyncGPUReadback.Request(motionBuffer));
		}
	}

	float GetFloat(int idx) {
		int x = idx / motionBuffer.height;
		int y = idx % motionBuffer.height;
		return colors[x + (motionBuffer.height-1-y) * motionBuffer.width].r;
	}


	HumanBodyBones[] humanBones;
	Transform[] bones;
	HumanUtil.BoneData[] boneData;

	HumanPoseHandler poseHandler;
	int[] muscleIds;
	float[] muscleMin;
	float[] muscleMax;

	float[] muscles;
	float humanScale;

	void Start() {
		humanBones = MeshGen.humanBodyBones;
		bones = new Transform[humanBones.Length];
		boneData = HumanUtil.LoadBoneData(animator, humanBones, bones);

		poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);

		muscleIds = new int[boneData.Sum(bd => bd.channels.Length)];
		muscleMin = new float[HumanTrait.MuscleCount];
		muscleMax = new float[HumanTrait.MuscleCount];
		var idx =  0;
		for(int i=0; i<bones.Length; i++)
			foreach(var j in boneData[i].channels) {
				if(j<3) {
					var humanBone = humanBones[i];
					var muscle = HumanTrait.MuscleFromBone((int)humanBone, j);
					if(muscle >= 0) {
						muscleMin[muscle] = boneData[i].axes.min[j];
						muscleMax[muscle] = boneData[i].axes.max[j];
					} else {
						switch(humanBone) {
						case HumanBodyBones.LeftShoulder:
							humanBone = HumanBodyBones.LeftUpperArm; break;
						case HumanBodyBones.RightShoulder:
							humanBone = HumanBodyBones.RightUpperArm; break;
						case HumanBodyBones.Jaw:
							break;
						default:
							humanBone = (HumanBodyBones)HumanTrait.GetParentBone((int)humanBone);break;
						}
						muscle = HumanTrait.MuscleFromBone((int)humanBone, j);

						// Debug.Log($"{humanBones[i]} axis {j} => {humanBone}");
					}
					muscleIds[idx] = muscle;
				}
				idx++;
			}
		
		muscles = new float[HumanTrait.MuscleCount];
		humanScale = HumanUtil.GetHumanScale(animator); 
	}

	void AnimateUsingHumanPose() {
		var rootT = new Vector3(0,1,0);
		var rootY = Vector3.up;
		var rootZ = Vector3.forward;

		var idx = 0;
		for(int i=0; i<bones.Length; i++) {
			foreach(var j in boneData[i].channels) {
				float range = j < 3 ? 180 : j < 6 ? 2 : 1;
				var v = Mathf.Lerp(-range, +range, GetFloat(idx));

				if(j<3) {
					if(muscleIds[idx] >= 0)
						muscles[muscleIds[idx]] += v;
				}
				else if(j<6)
					rootT[j-3] = v;
				else if(j<9)
					rootY[j-6] = v;
				else if(j<12)
					rootZ[j-9] = v;

				idx++;
			}
		}
		for(int i=0; i<HumanTrait.MuscleCount; i++)
			muscles[i] /= muscles[i] >= 0 ? muscleMax[i] : -muscleMin[i];

		var pose = new HumanPose{
			bodyPosition = rootT,
			bodyRotation = Quaternion.LookRotation(rootZ, rootY),
			muscles = muscles,
		};
		poseHandler.SetHumanPose(ref pose);
	}
	Quaternion muscleToRotation(Vector3 muscle) {
		var muscleYZ = new Vector3(0, muscle.y, muscle.z);
		return Quaternion.AngleAxis(muscleYZ.magnitude, muscleYZ.normalized)
				* Quaternion.AngleAxis(muscle.x, new Vector3(1,0,0));
	}
	void AnimateUsingRotation() {
		var root = animator.transform;
		var rootT = new Vector3(0,1,0);
		var rootY = Vector3.up;
		var rootZ = Vector3.forward;

		var idx = 0;
		for(int i=0; i<bones.Length; i++) {
			var muscle = Vector3.zero;
			foreach(var j in boneData[i].channels) {
				float range = j < 3 ? 180 : j < 6 ? 2 : 1;
				var v = Mathf.Lerp(-range, +range, GetFloat(idx));

				if(j<3)
					muscle[j] = v;
				else if(j<6)
					rootT[j-3] = v;
				else if(j<9)
					rootY[j-6] = v;
				else if(j<12)
					rootZ[j-9] = v;

				idx++;
			}
			if(!bones[i])
				continue;
			var axes = boneData[i].axes;

			if(humanBones[i] == HumanBodyBones.Hips)
				bones[i].SetPositionAndRotation(
					root.TransformPoint(rootT * humanScale),
					root.rotation * Quaternion.LookRotation(rootZ, rootY) * axes.preQ);
			else
				bones[i].localRotation = axes.preQ * muscleToRotation(axes.sign * muscle)
											* Quaternion.Inverse(axes.postQ);
		}
	}
}
}
#endif