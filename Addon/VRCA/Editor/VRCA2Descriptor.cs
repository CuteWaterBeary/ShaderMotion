#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace ShaderMotion.Addon {
class VRCA2Descriptor {
	public SerializedObject serializedObject;
	public static VRCA2Descriptor FromGameObject(GameObject go) {
		foreach(var mono in go.GetComponents<MonoBehaviour>()) {
			var so = new SerializedObject(mono);
			if(so.FindProperty("CustomStandingAnims") != null)
				return new VRCA2Descriptor{serializedObject=so};
		}
		return null;
	}

	public AnimatorOverrideController GetOverrideController() {
		return serializedObject.FindProperty("CustomStandingAnims").objectReferenceValue as AnimatorOverrideController;
	}
}
}
#endif