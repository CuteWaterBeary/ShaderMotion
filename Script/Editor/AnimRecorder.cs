#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;
using GameObjectRecorder = UnityEditor.Animations.GameObjectRecorder;
using System.Text.RegularExpressions;

namespace ShaderMotion {
[System.Serializable] // this is serializable to survive code reload
public class AnimRecorder {
	[SerializeField]
	Animator animator;
	Transform hips;
	GameObjectRecorder recorder;
	Transform[] proxies;

	// used to test if it's deserialized from null or not
	public static implicit operator bool(AnimRecorder r) {
		return !object.ReferenceEquals(r, null) && r.recorder;
	}
	public AnimRecorder(Animator animator) {
		this.animator = animator;
		this.hips     = animator.GetBoneTransform(HumanBodyBones.Hips);
		this.recorder = new GameObjectRecorder(animator.gameObject);
		this.proxies  = new Transform[HumanTrait.BoneCount];

		var hideFlags = HideFlags.DontSaveInEditor; // | HideFlags.HideInHierarchy
		var proxyRoot = EditorUtility.CreateGameObjectWithHideFlags("_bones_", hideFlags).transform;
		proxyRoot.SetParent(animator.transform, false);
		for(int i=0; i<HumanTrait.BoneCount; i++) {
			proxies[i] = EditorUtility.CreateGameObjectWithHideFlags(HumanTrait.BoneName[i], hideFlags).transform;
			proxies[i].SetParent(proxyRoot, false);
		}
		bindProxies();
	}
	public void BindComponent(Component component) {
		recorder.BindComponent(component);
	}
	public void Dispose() {
		var destroy = EditorApplication.isPlaying ? (System.Action<Object>)Object.Destroy : (System.Action<Object>)Object.DestroyImmediate;
		if(recorder) {
			recorder.ResetRecording();
			destroy(recorder);
		}
		if(proxies != null && proxies.Length != 0 && proxies[0])
			destroy(proxies[0].parent.gameObject);
		animator = null;
		recorder = null;
		hips = null;
		proxies = null;
	}
	void bindProxies() {
		{
			var path = AnimationUtility.CalculateTransformPath(proxies[0], animator.transform);
			for(int j=0; j<3; j++)
				recorder.Bind(EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition."+axes[j]));
			for(int j=0; j<4; j++)
				recorder.Bind(EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation."+axes[j]));
		}
		for(int i=1; i<HumanTrait.BoneCount; i++) if(animator.GetBoneTransform((HumanBodyBones)i)) {
			var path = AnimationUtility.CalculateTransformPath(proxies[i], animator.transform);
			for(int j=0; j<3; j++)
				if(MuscleFromBone[i, j] >= 0)
					recorder.Bind(EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition."+axes[j]));
		}
	}
	void getProxyCurves(AnimationClip clip, AnimationCurve[] rootCurves, AnimationCurve[] muscleCurves) {
		{
			var path = AnimationUtility.CalculateTransformPath(proxies[0], animator.transform);
			for(int j=0; j<3; j++)
				rootCurves[0+j] = AnimationUtility.GetEditorCurve(clip,
					EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition."+axes[j]));
			for(int j=0; j<4; j++)
				rootCurves[3+j] = AnimationUtility.GetEditorCurve(clip,
					EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation."+axes[j]));
			clip.SetCurve(path, typeof(Transform), "m_LocalRotation", null);
		}
		for(int i=1; i<HumanTrait.BoneCount; i++) {
			var path = AnimationUtility.CalculateTransformPath(proxies[i], animator.transform);
			for(int j=0; j<3; j++)
				if(MuscleFromBone[i, j] >= 0)
					muscleCurves[MuscleFromBone[i, j]] = AnimationUtility.GetEditorCurve(clip,
						EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition."+axes[j]));
		}
		// somehow it's faster to remove curves together at end
		for(int i=0; i<HumanTrait.BoneCount; i++) {
			var path = AnimationUtility.CalculateTransformPath(proxies[i], animator.transform);
			clip.SetCurve(path, typeof(Transform), "m_LocalPosition", null); // fast remove xyz in a single call
		}
	}

	[System.NonSerialized]
	private HumanPose humanPose;
	private HumanPoseHandler humanPoseHandler;
	public void TakeSnapshot(float deltaTime) {
		if(humanPoseHandler == null)
			humanPoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
		humanPoseHandler.GetHumanPose(ref humanPose);

		(proxies[0].localPosition, proxies[0].localRotation) = HumanPoser.GetRootMotion(ref humanPose, animator);
		for(int i=1; i<HumanTrait.BoneCount; i++) {
			var pos = Vector3.zero;
			for(int j=0; j<3; j++)
				if(MuscleFromBone[i, j] >= 0)
					pos[j] = humanPose.muscles[MuscleFromBone[i, j]];
			proxies[i].localPosition = pos;
		}
		recorder.TakeSnapshot(deltaTime);
	}
	public void SaveToClip(AnimationClip clip, float fps=60) {
		if(!recorder.isRecording) {
			clip.ClearCurves();
			clip.frameRate = fps;
			return;
		}

		recorder.SaveToClip(clip, fps);

		var rootCurves = new AnimationCurve[7];
		var muscleCurves = new AnimationCurve[HumanTrait.MuscleCount];
		getProxyCurves(clip, rootCurves, muscleCurves);
		RemoveConstantCurves(clip);

		var so = new SerializedObject(clip);
		// don't change root motion (BakeIntoPose = true)
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendOrientation").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendPositionY").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_LoopBlendPositionXZ").boolValue = true;
		// set coordinate system to origin (BasedUpon = origin)
		so.FindProperty("m_AnimationClipSettings.m_KeepOriginalOrientation").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_KeepOriginalPositionY").boolValue = true;
		so.FindProperty("m_AnimationClipSettings.m_KeepOriginalPositionXZ").boolValue = true;
		so.ApplyModifiedProperties();

		SetHumanCurves(clip, rootCurves, muscleCurves);
	}

	static void SetHumanCurves(AnimationClip clip, AnimationCurve[] rootCurves, AnimationCurve[] muscleCurves) {
		// AnimationClip.SetCurve is faster than AnimationUtility.SetEditorCurve
		for(int i=0; i<3; i++)
			clip.SetCurve("", typeof(Animator), "RootT."+axes[i], rootCurves[0+i]);
		for(int i=0; i<4; i++)
			clip.SetCurve("", typeof(Animator), "RootQ."+axes[i], rootCurves[3+i]);
		for(int i=0; i<HumanTrait.MuscleCount; i++)
			clip.SetCurve("", typeof(Animator), MuscleName[i], muscleCurves[i]);
	}
	static void RemoveConstantCurves(AnimationClip clip) {
		foreach(var binding in AnimationUtility.GetCurveBindings(clip)) {
			var curve = AnimationUtility.GetEditorCurve(clip, binding);
			if(curve.length <= 2 && curve.keys[0].value == curve.keys[curve.length-1].value)
				clip.SetCurve(binding.path, binding.type, binding.propertyName, null);
		}
		foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
			var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
			if(keys.Length <= 2 && keys[0].value == keys[keys.Length-1].value)
				AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
		}
	}

	static readonly string[] axes = new[]{"x", "y", "z", "w"};
	// patch MuscleName to match humanoid animation property name
	public static readonly string[] MuscleName = HumanTrait.MuscleName.Select(name => Regex.Replace(name,
			@"(Left|Right) (\w+) (Spread|\w+ Stretched)", "$1Hand.$2.$3", RegexOptions.IgnoreCase)).ToArray();
	// cache MuscleFromBone for fast access in TakeSnapshot
	public static readonly int[,]   MuscleFromBone = new int[HumanTrait.BoneCount, 3];
	static AnimRecorder() {
		for(int i=0; i<HumanTrait.BoneCount; i++)
			for(int j=0; j<3; j++)
				MuscleFromBone[i,j] = HumanTrait.MuscleFromBone(i, j);
	}
}
class AnimRecorderWindow : EditorWindow {
	[MenuItem("CONTEXT/Animator/RecordAnimation")]
	static void RecordAnimation(MenuCommand command) {
		var animator = (Animator)command.context;
		var window = EditorWindow.GetWindow<AnimRecorderWindow>("RecordAnimation");
		window.Show();
		window.animator = animator;
	}

	AnimRecorder recorder = null;
	Animator animator = null;
	AnimationClip clip = null;
	int frameRate = 30;
	void OnGUI() {
		animator = (Animator)EditorGUILayout.ObjectField("Animator", animator, typeof(Animator), true);
		clip = (AnimationClip)EditorGUILayout.ObjectField("Output clip", clip, typeof(AnimationClip), false);
		frameRate = EditorGUILayout.IntSlider("Frame rate", frameRate, 1, 120);

		if(!recorder) {
			using(new EditorGUI.DisabledScope(!animator || !clip || !EditorApplication.isPlaying))
				if(GUILayout.Button("Start")) {
					recorder = new AnimRecorder(animator);
					foreach(var smr in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
						recorder.BindComponent(smr);
				}
		} else if(!EditorApplication.isPlaying) {
			recorder.Dispose();
			recorder = null;
		} else {
			if(GUILayout.Button("Stop")) {
				clip.ClearCurves();
				recorder.SaveToClip(clip, frameRate);
				recorder.Dispose();
				recorder = null;
				AssetDatabase.SaveAssets();
			}
		}
	}
	void Update() {
		if(!EditorApplication.isPlaying || EditorApplication.isPaused)
			return;
		if(recorder)
			recorder.TakeSnapshot(Time.deltaTime);
	}
}
}
#endif