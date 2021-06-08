#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEditor;
using UnityEditor.Animations;

namespace ShaderMotion.Addon {
class VRCARecorder {
	[MenuItem("ShaderMotion/TestDel")]
	static void TestDel() {
		var o = Selection.activeObject;
		var path = AssetDatabase.GetAssetPath(o)+"."+o.name+".asset";
		AssetDatabase.RemoveObjectFromAsset(o);
		AssetDatabase.CreateAsset(o, path);
		// Object.DestroyImmediate(Selection.activeObject);
	}
	[MenuItem("CONTEXT/Animator/CreateVRCARecorder")]
	static void CreateRecorder(MenuCommand command) {
		var animator = (Animator)command.context;
		CreateRecorder(animator);
		SetupAvatar(animator);
	}
	static void CreateRecorder(Animator animator) {
		const string helperName = "Helper";

		var recorder = MeshRecorder.CreateRecorderSkinned(animator);
		var helper0 = recorder.transform.Find(helperName)?.gameObject;
		var helper = helper0 ?? (GameObject)PrefabUtility.InstantiatePrefab(
									Resources.Load<GameObject>("VRCARecorderHelper"));
		if(!helper0) {
			helper.name = helperName;
			helper.transform.SetParent(recorder.transform, false);
		}

		// setup camera
		var cam = helper.GetComponentInChildren<Camera>();
		cam.GetComponent<PositionConstraint>().SetSource(0, new ConstraintSource{
			sourceTransform = animator.GetBoneTransform(HumanBodyBones.Hips), weight=1});

		// setup recorder
		recorder.rootBone = helper.GetComponentInChildren<ParentConstraint>().transform;
		recorder.updateWhenOffscreen = true;
		if((cam.cullingMask & (1<<recorder.gameObject.layer)) == 0) // fix layer
			for(int i=0; i<31; i++)
				if((cam.cullingMask & (1<<i)) != 0) {
					recorder.gameObject.layer = i;
					break;
				}
	}
	static void SetupAvatar(Animator animator) {
		var desc3 = VRCA3Descriptor.FromGameObject(animator.gameObject);
		if(desc3 != null) {
			if(!desc3.AddAnimationLayer(VRCA3Descriptor.FX, Resources.Load<AnimatorController>("VRCARecorderFX")))
				Debug.LogError("VRCA3 Playable layers are missing. Please click 'Customize' button");
			if(!desc3.AddExpressions(Resources.Load<ScriptableObject>("VRCARecorderMenu"),
					Resources.Load<ScriptableObject>("VRCARecorderParams")))
				Debug.LogError("VRCA3 Expressions are missing. Please click 'Customize' button");
			return;
		}
		var desc2 = VRCA2Descriptor.FromGameObject(animator.gameObject);
		if(desc2 != null) {
			var overrideController = desc2.GetOverrideController();
			if(!overrideController) {
				Debug.LogError("VRCA2 Override controller is missing");
				return;
			}
			overrideController["EMOTE8"] = Resources.Load<AnimatorController>("VRCARecorderFX")
				.animationClips.FirstOrDefault(x => x.name == "Calibrate");
			Debug.Log($"{overrideController} is updated");
			return;
		}
		Debug.LogError("Avatar descriptor is missing");
	}
}
}
#endif