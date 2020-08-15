#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;
using GameObjectRecorder = UnityEditor.Animations.GameObjectRecorder;

namespace ShaderMotion {
public class HumanAnimatorRecorder {
	static string[] axes = new[]{"x", "y", "z", "w"};
	static int[,] boneMuscles = new int[HumanTrait.BoneCount, 3];
	static HumanAnimatorRecorder() {
		for(int i=0; i<HumanTrait.BoneCount; i++)
			for(int j=0; j<3; j++)
				boneMuscles[i,j] = HumanTrait.MuscleFromBone(i, j);
	}

	Animator animator;
	Transform hips;
	HumanPoseHandler poseHandler;
	HumanPose humanPose = new HumanPose();
	GameObjectRecorder recorder;
	Transform[] surrogates;
	float bodyScale;

	public HumanAnimatorRecorder(Animator animator) {
		this.animator    = animator;
		this.hips        = animator.GetBoneTransform(HumanBodyBones.Hips);
		this.poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
		this.recorder    = new GameObjectRecorder(animator.gameObject);
		this.surrogates  = new Transform[HumanTrait.BoneCount];

		var hideFlags = HideFlags.DontSaveInEditor; // | HideFlags.HideInHierarchy
		var surrogateRoot = EditorUtility.CreateGameObjectWithHideFlags("_bones_", hideFlags).transform;
		surrogateRoot.SetParent(animator.transform, false);
		for(int i=0; i<HumanTrait.BoneCount; i++) {
			surrogates[i] = EditorUtility.CreateGameObjectWithHideFlags(HumanTrait.BoneName[i], hideFlags).transform;
			surrogates[i].SetParent(surrogateRoot, false);
			recorder.BindComponent(surrogates[i]);
		}
	}
	public void Close() {
		var destroy = EditorApplication.isPlaying ? (System.Action<Object>)Object.Destroy : (System.Action<Object>)Object.DestroyImmediate;
		if(recorder) {
			recorder.ResetRecording();
			destroy(recorder);
		}
		if(surrogates[0])
			destroy(surrogates[0].parent.gameObject);	
	}
	public void TakeSnapshot(float deltaTime) {
		poseHandler.GetHumanPose(ref humanPose);

		surrogates[0].rotation = humanPose.bodyRotation;
		surrogates[0].localPosition =
			animator.transform.InverseTransformVector(Vector3.Scale(hips.parent.lossyScale, 
				humanPose.bodyPosition * animator.humanScale - animator.transform.position));

		for(int i=1; i<HumanTrait.BoneCount; i++) {
			var pos = Vector3.zero;
			for(int j=0; j<3; j++)
				if(boneMuscles[i, j] >= 0)
					pos[j] = humanPose.muscles[boneMuscles[i, j]];
			surrogates[i].localPosition = pos;
		}
		recorder.TakeSnapshot(deltaTime);
	}
	public void SaveToClip(AnimationClip clip, float fps=60) {
		if(!recorder.isRecording)
			return;

		recorder.SaveToClip(clip, fps);

		var rootTCurves = new AnimationCurve[3];
		var rootQCurves = new AnimationCurve[4];
		{
			var path = AnimationUtility.CalculateTransformPath(surrogates[0], animator.transform);
			for(int j=0; j<3; j++)
				rootTCurves[j] = AnimationUtility.GetEditorCurve(clip,
					EditorCurveBinding.FloatCurve(path, typeof(Transform), $"m_LocalPosition.{axes[j]}"));
			for(int j=0; j<4; j++)
				rootQCurves[j] = AnimationUtility.GetEditorCurve(clip,
					EditorCurveBinding.FloatCurve(path, typeof(Transform), $"m_LocalRotation.{axes[j]}"));
		}
		var muscleCurves = new AnimationCurve[HumanTrait.MuscleCount];
		for(int i=1; i<HumanTrait.BoneCount; i++) {
			var path = AnimationUtility.CalculateTransformPath(surrogates[i], animator.transform);
			for(int j=0; j<3; j++)
				if(boneMuscles[i, j] >= 0)
					muscleCurves[boneMuscles[i, j]] = AnimationUtility.GetEditorCurve(clip,
						EditorCurveBinding.FloatCurve(path, typeof(Transform), $"m_LocalPosition.{axes[j]}"));
		}

		clip.ClearCurves();

		// set BakeIntoPose = true, BasedUpon = origin
		var so = new SerializedObject(clip);
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendOrientation").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendPositionY").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendPositionXZ").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_KeepOriginalOrientation").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_KeepOriginalPositionY").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_KeepOriginalPositionXZ").boolValue = true;
		so.ApplyModifiedProperties();

		for(int i=0; i<3; i++)
			AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(
				"", typeof(Animator), $"RootT.{axes[i]}"), rootTCurves[i]);
		for(int i=0; i<4; i++)
			AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(
				"", typeof(Animator), $"RootQ.{axes[i]}"), rootQCurves[i]);
		for(int i=0; i<HumanTrait.MuscleCount; i++)
			AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(
				"", typeof(Animator), HumanTrait.MuscleName[i]), muscleCurves[i]);
	}
}
class HumanAnimatorRecorderEW : EditorWindow {
	[MenuItem("CONTEXT/Animator/RecordAnimation")]
	static void RecordAnimation(MenuCommand command) {
		var animator = (Animator)command.context;
		var window = EditorWindow.GetWindow<HumanAnimatorRecorderEW>("RecordAnimation");
		window.Show();
		window.animator = animator;
	}

	HumanAnimatorRecorder recorder = null;
	Animator animator = null;
	AnimationClip clip = null;
	int frameRate = 30;
	void OnGUI() {
		animator = (Animator)EditorGUILayout.ObjectField("Animator", animator, typeof(Animator), true);
		clip = (AnimationClip)EditorGUILayout.ObjectField("Clip", clip, typeof(AnimationClip), false);
		frameRate = EditorGUILayout.IntSlider("Frame rate", frameRate, 1, 120);

		if(recorder == null) {
			if(GUILayout.Button("Start")) {
				recorder = new HumanAnimatorRecorder(animator);
			}
		} else if(!EditorApplication.isPlaying) {
			recorder.Close();
			recorder = null;
		} else {
			if(GUILayout.Button("Stop")) {
				clip.ClearCurves();
				recorder.SaveToClip(clip, frameRate);
				recorder.Close();
				recorder = null;
				AssetDatabase.SaveAssets();
			}
		}
	}
	void Update() {
		if(!EditorApplication.isPlaying || EditorApplication.isPaused)
			return;
		if(recorder != null)
			recorder.TakeSnapshot(Time.deltaTime);
	}
}
}
#endif