#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEditor;
using UnityEditor.Animations;
using System.Text.RegularExpressions;

namespace ShaderMotion.Addon {
class VRCASetup : EditorWindow {
	[MenuItem("CONTEXT/Animator/SetupAvatar")]
	static void SetupAvatar(MenuCommand command) {
		var window = EditorWindow.GetWindow<VRCASetup>("SetupAvatar");
		window.animator = (Animator)command.context;
		window.Show();
	}
	Animator animator;
	void OnGUI() {
		animator = (Animator)EditorGUILayout.ObjectField("Avatar", animator, typeof(Animator), true);
		using(new EditorGUI.DisabledScope(!(animator && animator.isHuman))) {
			if(GUILayout.Button("Setup Motion Recorder"))
				CreateRecorder(animator);
			if(GUILayout.Button("Setup Motion Player"))
				CreatePlayer(animator);
			if(GUILayout.Button("Setup Animator")) {
				SetupPrefab(animator);
				SetupDescriptor(animator);
			}
		}
	}

	public static SkinnedMeshRenderer CreateRecorder(Animator animator) {
		var recorder = MeshRecorder.CreateRecorderSkinned(
			MeshRecorder.CreateChild(animator, "Recorder"),
			MeshRecorder.CreatePath(animator, "Recorder"),
			animator);

		recorder.updateWhenOffscreen = true;
		return recorder;
	}
	public static MeshRenderer CreatePlayer(Animator animator) {
		var player = MeshPlayer.CreatePlayer(
			MeshRecorder.CreateChild(animator, "Player"),
			MeshRecorder.CreatePath(animator, "Player"),
			animator, animator.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>()
				.Where(smr => !Regex.IsMatch(smr.name, "(Recorder|Player)$")).ToArray());

		var shader = Resources.Load<Material>("SMVRCAPlayer").shader;
		var mesh = player.GetComponent<MeshFilter>().sharedMesh;
		mesh.bounds = new Bounds(Vector3.zero,
			Vector3.Min(mesh.bounds.size, new Vector3(2.5f, 2.5f, 2.5f)));
		foreach(var mat in player.sharedMaterials)
			mat.shader = shader;
		return player;
	}
	public static void SetupPrefab(Animator animator) {
		var prefab = Resources.Load<GameObject>("SMVRCA");
		var root = animator.transform.Find(prefab.name)?.gameObject
					?? (GameObject)PrefabUtility.InstantiatePrefab(prefab);
		root.transform.SetParent(animator.transform, false);

		var anchor = root.GetComponentInChildren<ParentConstraint>().transform;
		var hips = animator.GetBoneTransform(HumanBodyBones.Hips);

		// setup camera
		var cam = root.GetComponentInChildren<Camera>();
		cam.GetComponent<PositionConstraint>().SetSource(0, new ConstraintSource{sourceTransform=hips, weight=1});

		// setup recorder
		var recorder = animator.transform.Find("Recorder")?.GetComponent<SkinnedMeshRenderer>();
		if(recorder) {
			recorder.rootBone = anchor;
			
			if((cam.cullingMask & (1<<recorder.gameObject.layer)) == 0) // fix layer
				for(int i=0; i<31; i++)
					if((cam.cullingMask & (1<<i)) != 0) {
						recorder.gameObject.layer = i;
						break;
					}
		}

		// setup player
		var player = animator.transform.Find("Player")?.GetComponent<MeshRenderer>();
		if(player) {
			var constraint = player.GetComponent<ParentConstraint>();
			if(!constraint)
				constraint = player.gameObject.AddComponent<ParentConstraint>();
			EditorUtility.CopySerialized(anchor.GetComponent<ParentConstraint>(), constraint);
			constraint.SetSource(0, new ConstraintSource{sourceTransform=anchor, weight=1});
			constraint.constraintActive = true;
		}
	}
	public static void SetupDescriptor(Animator animator) {
		var desc3 = VRCA3Descriptor.FromGameObject(animator.gameObject);
		if(desc3 != null) {
			desc3.MergeAnimationLayer(VRCA3Descriptor.FX, Resources.Load<AnimatorController>("SMVRCAFX"));
			desc3.MergeExpressions(Resources.Load<ScriptableObject>("SMVRCAMenu"),
					Resources.Load<ScriptableObject>("SMVRCAParams"));
			return;
		}
		var desc2 = VRCA2Descriptor.FromGameObject(animator.gameObject);
		if(desc2 != null) {
			var overrideController = desc2.GetOverrideController();
			if(!overrideController) {
				Debug.LogError("VRCA2 Override controller is missing");
				return;
			}
			overrideController["EMOTE8"] = Resources.Load<AnimatorController>("SMVRCAFX")
				.animationClips.FirstOrDefault(x => x.name == "Calibrate");
			Debug.Log($"{overrideController} is updated");
			return;
		}
		Debug.LogError("Avatar descriptor is missing");
	}
}
}
#endif