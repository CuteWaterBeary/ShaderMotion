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
	GPUReader gpuReader = new GPUReader();
	public RenderTexture motionBuffer;
	public Animator animator;
	public bool useRawMuscle = false;
	

	NativeArray<Color> colors = new NativeArray<Color>();

	void Update() {
		var request = gpuReader.Request(motionBuffer);
		if(request != null && !request.Value.hasError) {
			colors = request.Value.GetData<Color>();
			if(useRawMuscle)
				AnimateUsingHumanPose();
			else
				AnimateUsingRotation();
			if(recording)
				recorder.TakeSnapshot(Time.deltaTime);
			else if(recording_) {
				if(!recordingClip) {
					recordingClip = new AnimationClip();
					var path0 = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)),
									animator.name);
					AssetDatabase.CreateAsset(recordingClip, $"{path0}_motion.anim");
				}
				recordingClip.ClearCurves();
				recorder.SaveToClip(recordingClip);
				recorder.ResetRecording();
				AssetDatabase.SaveAssets();
			}
			recording_ = recording;
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

	bool recording_ = false;
	public bool recording = false;
	public AnimationClip recordingClip;
	HumanAnimatorRecorder recorder;
	

	void Start() {
		recorder = new HumanAnimatorRecorder(animator);
	
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

		Array.Clear(muscles, 0, muscles.Length);

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
					root.rotation * Quaternion.LookRotation(rootZ, rootY) * Quaternion.Inverse(axes.postQ));
			else
				bones[i].localRotation = axes.preQ * muscleToRotation(axes.sign * muscle)
											* Quaternion.Inverse(axes.postQ);
		}
	}
	// [CustomEditor(typeof(AnimatorPlayer))]
	// class CustomEditor : Editor {
	// 	public override void OnInspectorGUI() {
	// 		base.OnInspectorGUI();
	// 		outputClip
	// 		if(GUILayout.Button("StartRecording")) {

 //            		// 		var clip = new AnimationClip();
	// // 		recorder.SaveToClip(clip);
			

	// // 		var path0 = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)),
	// // 						animator.name);
	// // 		var path = $"{path0}_motion.anim";
	// // 		AssetDatabase.CreateAsset(clip, path);
 //        	}
	// 	}
	// }
}
}
#endif