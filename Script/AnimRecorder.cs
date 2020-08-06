#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
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
		}
		ResetRecording();
	}
	public void ResetRecording() {
		recorder.ResetRecording();
		for(int i=0; i<HumanTrait.BoneCount; i++)
			recorder.BindComponent(surrogate[i]);
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
	public void SaveToClip(AnimationClip clip) {
		recorder.SaveToClip(clip);

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
}
#endif