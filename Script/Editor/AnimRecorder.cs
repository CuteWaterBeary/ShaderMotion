#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using GameObjectRecorder = UnityEditor.Animations.GameObjectRecorder;
using System.Text.RegularExpressions;

namespace ShaderMotion {
[System.Serializable] // make this serializable to survive code reload
public class AnimRecorder {
	[SerializeField]
	Animator animator;
	GameObjectRecorder recorder;
	Transform[] proxies;

	// used to test if it's deserialized from null or not
	public static implicit operator bool(AnimRecorder r) {
		return !object.ReferenceEquals(r, null) && r.recorder;
	}
	public AnimRecorder(Animator animator) {
		this.animator = animator;
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
		if((proxies?.Length??0) != 0 && proxies[0])
			destroy(proxies[0].parent.gameObject);
		animator = null;
		recorder = null;
		proxies  = null;
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

		// todo: use CurveFilterOptions in 2019
		recorder.SaveToClip(clip, fps);

		var rootCurves = new AnimationCurve[7];
		var muscleCurves = new AnimationCurve[HumanTrait.MuscleCount];
		getProxyCurves(clip, rootCurves, muscleCurves);
		RemoveConstantCurves(clip);
		SetHumanCurves(clip, rootCurves, muscleCurves);

		var settings = AnimationUtility.GetAnimationClipSettings(clip);
		settings.loopBlendOrientation    = true; // Root Transform Rotation: Bake Into Pose
		settings.keepOriginalOrientation = true; // Root Transform Rotation: Based Upon = Origin
		settings.orientationOffsetY      = 0;    // Root Transform Rotation: Offset
		settings.loopBlendPositionY      = true; // Root Transform Position (Y): Bake Into Pose
		settings.keepOriginalPositionY   = true; // Root Transform Position (Y): Based Upon = Origin
		settings.level                   = 0;    // Root Transform Position (Y): Offset
		settings.loopBlendPositionXZ     = true; // Root Transform Position (XZ): Bake Into Pose
		settings.keepOriginalPositionXZ  = true; // Root Transform Position (XZ): Based Upon = Origin
		AnimationUtility.SetAnimationClipSettings(clip, settings);
	}

	static void SetHumanCurves(AnimationClip clip, AnimationCurve[] rootCurves, AnimationCurve[] muscleCurves) {
		// AnimationClip.SetCurve is faster than AnimationUtility.SetEditorCurve
		for(int i=0; i<3; i++)
			clip.SetCurve("", typeof(Animator), "RootT."+axes[i], rootCurves[0+i]);
		for(int i=0; i<4; i++)
			clip.SetCurve("", typeof(Animator), "RootQ."+axes[i], rootCurves[3+i]);
		for(int i=0; i<HumanTrait.MuscleCount; i++)
			clip.SetCurve("", typeof(Animator), MusclePropName[i], muscleCurves[i]);
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
	public static readonly int[,]   MuscleFromBone = new int[HumanTrait.BoneCount, 3];
	public static readonly string[] MusclePropName = HumanTrait.MuscleName.Select(name => Regex.Replace(name,
			@"(Left|Right) (\w+) (Spread|\w+ Stretched)", "$1Hand.$2.$3", RegexOptions.IgnoreCase)).ToArray();
	static AnimRecorder() {
		for(int i=0; i<HumanTrait.BoneCount; i++)
			for(int j=0; j<3; j++)
				MuscleFromBone[i,j] = HumanTrait.MuscleFromBone(i, j);
	}
}
class AnimRecorderWindow : EditorWindow {
	[MenuItem("CONTEXT/Animator/RecordAnimation")]
	static void RecordAnimation(MenuCommand command) {
		var window = EditorWindow.GetWindow<AnimRecorderWindow>("RecordAnimation");
		window.animator = (Animator)command.context;
		window.Show();
	}

	AnimRecorder recorder;
	Animator animator;
	AnimationClip clip;
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