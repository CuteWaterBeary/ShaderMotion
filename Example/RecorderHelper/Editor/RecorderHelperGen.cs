#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;

namespace ShaderMotion {
public class RecorderHelperGen {
	public static GameObject CreateRecorderHelper(GameObject helper, Animator animator, SkinnedMeshRenderer smr) {
		if(!helper) {
			helper = (GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("RecorderHelper"),
									animator.transform);
		}

		var cam = helper.GetComponentInChildren<Camera>();
		var anchor = helper.GetComponentInChildren<ParentConstraint>();
		cam.GetComponent<PositionConstraint>().SetSource(0, new ConstraintSource{
			sourceTransform = animator.GetBoneTransform(HumanBodyBones.Hips), weight=1});
		
		var humanScale = HumanUtil.GetHumanScale(animator);
		var bounds = smr.sharedMesh.bounds;
		smr.rootBone = anchor.transform;
		smr.rootBone.localScale = humanScale * new Vector3(1,1,1);
		smr.localBounds = new Bounds(bounds.center/humanScale, bounds.size/humanScale);
		return helper;
	}
	[MenuItem("ShaderMotion/Create Recorder Helper")]
	static void CreateRecorderHelper() {
		var smr = Selection.activeGameObject.GetComponentInParent<SkinnedMeshRenderer>();
		if(!smr) {
			Debug.LogError($"Expect a recorder SkinnedMeshRenderer on {Selection.activeGameObject}");
			return;
		}
		var animator = smr.gameObject.GetComponentInParent<Animator>();

		var helper0 = animator.transform.Find("RecorderHelper")?.gameObject;
		var helper = CreateRecorderHelper(helper0, animator, smr);
		if(!helper0)
			helper.name = "RecorderHelper";
	}
}
}
#endif