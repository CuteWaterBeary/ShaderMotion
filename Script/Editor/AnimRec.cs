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
	HumanPoseHandler poseHandler;
	HumanPose humanPose = new HumanPose();
	GameObjectRecorder recorder;
	Transform[] surrogate;

	public HumanAnimatorRecorder(Animator animator) {
		this.animator    = animator;
		this.poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
		this.recorder    = new GameObjectRecorder(animator.gameObject);
		this.surrogate   = new Transform[HumanTrait.BoneCount];
		for(int i=0; i<HumanTrait.BoneCount; i++) {
			var hideFlags = HideFlags.DontSaveInEditor; // | HideFlags.HideInHierarchy
			surrogate[i] = EditorUtility.CreateGameObjectWithHideFlags($"_{i}", hideFlags).transform;
			surrogate[i].SetParent(animator.transform, false);
			recorder.BindComponent(surrogate[i]);
		}
	}
	public void Close() {
		recorder.ResetRecording();
		Object.Destroy(recorder);
		for(int i=0; i<HumanTrait.BoneCount; i++)
			Object.Destroy(surrogate[i].gameObject);
	}
	public void TakeSnapshot(float deltaTime) {
		poseHandler.GetHumanPose(ref humanPose);
		surrogate[0].SetPositionAndRotation(humanPose.bodyPosition, humanPose.bodyRotation);
		for(int i=1; i<HumanTrait.BoneCount; i++) {
			var pos = Vector3.zero;
			for(int j=0; j<3; j++)
				if(boneMuscles[i, j] >= 0)
					pos[j] = humanPose.muscles[boneMuscles[i, j]];
			surrogate[i].localPosition = pos;
		}
		recorder.TakeSnapshot(deltaTime);
	}
	public void SaveToClip(AnimationClip clip, float fps=60) {
		recorder.SaveToClip(clip, fps);

		var rootTCurves = new AnimationCurve[3];
		var rootQCurves = new AnimationCurve[4];
		{
			var path = AnimationUtility.CalculateTransformPath(surrogate[0], animator.transform);
			for(int j=0; j<3; j++)
				rootTCurves[j] = AnimationUtility.GetEditorCurve(clip,
					EditorCurveBinding.FloatCurve(path, typeof(Transform), $"m_LocalPosition.{axes[j]}"));
			for(int j=0; j<4; j++)
				rootQCurves[j] = AnimationUtility.GetEditorCurve(clip,
					EditorCurveBinding.FloatCurve(path, typeof(Transform), $"m_LocalRotation.{axes[j]}"));
		}
		var muscleCurves = new AnimationCurve[HumanTrait.MuscleCount];
		for(int i=1; i<HumanTrait.BoneCount; i++) {
			var path = AnimationUtility.CalculateTransformPath(surrogate[i], animator.transform);
			for(int j=0; j<3; j++)
				if(boneMuscles[i, j] >= 0)
					muscleCurves[boneMuscles[i, j]] = AnimationUtility.GetEditorCurve(clip,
						EditorCurveBinding.FloatCurve(path, typeof(Transform), $"m_LocalPosition.{axes[j]}"));
		}

		clip.ClearCurves();

		// set BakeIntoPose = true
		var so = new SerializedObject(clip);
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendOrientation").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendPositionY").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendPositionXZ").boolValue = true;
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
class HumanAnimatorRecorderEditor : EditorWindow {
	[MenuItem("ShaderMotion/Record Humanoid Animation")]
	static void Init() {
		var window = EditorWindow.GetWindow<HumanAnimatorRecorderEditor>("Record Humanoid Animation");
		window.Show();
	}

	HumanAnimatorRecorder recorder = null;
	Animator animator = null;
	AnimationClip clip = null;
	int frameRate = 60;
	string path = null;
	void OnGUI() {
		animator = (Animator)EditorGUILayout.ObjectField("Animator", animator, typeof(Animator), true);
		clip = (AnimationClip)EditorGUILayout.ObjectField("Output clip", clip, typeof(AnimationClip), false);
		
		if(clip && AssetDatabase.IsMainAsset(clip))
			path = AssetDatabase.GetAssetPath(clip);
		else if(string.IsNullOrEmpty(path) && animator)
			path = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)),
									$"{animator.name}_rec.anim");
		var areaStyle = new GUIStyle(GUI.skin.textArea);
		areaStyle.wordWrap = true;
		path = EditorGUILayout.TextField("Output clip path", path, areaStyle, GUILayout.ExpandHeight(true));
		frameRate = EditorGUILayout.IntField("Frame rate", frameRate);

		EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
		if(recorder == null) {
			if(GUILayout.Button("Start")) {
				recorder = new HumanAnimatorRecorder(animator);
			}
		} else if(!EditorApplication.isPlaying) {
			recorder.Close();
			recorder = null;
		} else {
			if(GUILayout.Button("Stop")) {
				var newClip = false;
				if(!clip) {
					clip = new AnimationClip();
					newClip = true;
				}
				clip.ClearCurves();
				recorder.SaveToClip(clip, frameRate);
				recorder.Close();
				recorder = null;
				if(newClip)
					AssetDatabase.CreateAsset(clip, path);
				else
					AssetDatabase.SaveAssets();
			}
		}
		EditorGUI.EndDisabledGroup();
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