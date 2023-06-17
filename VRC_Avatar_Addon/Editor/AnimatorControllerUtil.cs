#if UNITY_EDITOR
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace ShaderMotion.Addon {
class AnimatorControllerUtil {
	public static void CopyLayers(AnimatorController srcController, AnimatorController dstController) {
		var srcLayers = srcController.layers;
		var dstLayers = dstController.layers;
		for(int srcIndex = 0; srcIndex < srcLayers.Length; ++srcIndex) {
			var dstIndex = dstLayers.TakeWhile(x => x.name != srcLayers[srcIndex].name).Count();
			if(dstIndex == dstLayers.Length) {
				dstController.AddLayer(srcLayers[srcIndex].name);
				dstLayers = dstController.layers;
			}
			CopyLayer(srcLayers[srcIndex], srcController, srcIndex, dstLayers[dstIndex], dstController, dstIndex);
			dstController.layers = dstLayers;
		}
	}
	public static void CopyParameters(AnimatorController srcController, AnimatorController dstController) {
		var srcParams = srcController.parameters;
		var dstParams = dstController.parameters;
		for(int srcIndex = 0; srcIndex < srcParams.Length; ++srcIndex) {
			var dstIndex = dstParams.TakeWhile(x => x.name != srcParams[srcIndex].name).Count();
			if(dstIndex == dstParams.Length) {
				dstController.AddParameter(srcParams[srcIndex].name, srcParams[srcIndex].type);
				dstParams = dstController.parameters;
			}
			CopyParameter(srcParams[srcIndex], dstParams[dstIndex]);
			dstController.parameters = dstParams;
		}
	}
	static void CopyLayer(AnimatorControllerLayer srcLayer, AnimatorController srcController, int srcIndex,
							AnimatorControllerLayer dstLayer, AnimatorController dstController, int dstIndex) {
		dstLayer.avatarMask    = srcLayer.avatarMask;
		dstLayer.blendingMode  = srcLayer.blendingMode;
		dstLayer.defaultWeight = srcLayer.defaultWeight;
		dstLayer.iKPass        = srcLayer.iKPass;
		Debug.Assert(srcLayer.syncedLayerIndex < 0, "syncedLayer and overrides are not implemented");

		var srcMachine = srcLayer.stateMachine;
		var dstMachine = dstLayer.stateMachine;
		AnimatorStateMachine_Clear.Invoke(dstMachine, new object[]{});
		Unsupported.CopyStateMachineDataToPasteboard(srcMachine, srcController, srcIndex);
		Unsupported.PasteToStateMachineFromPasteboard(dstMachine, dstController, dstIndex, default(Vector3));
		dstLayer.stateMachine = dstMachine.stateMachines[0].stateMachine;
		Undo.DestroyObjectImmediate(dstMachine);
	}
	static void CopyParameter(AnimatorControllerParameter srcParam, AnimatorControllerParameter dstParam) {
		dstParam.defaultBool  = srcParam.defaultBool;
		dstParam.defaultFloat = srcParam.defaultFloat;
		dstParam.defaultInt   = srcParam.defaultInt;
		dstParam.type         = srcParam.type;
	}
	static readonly MethodInfo AnimatorStateMachine_Clear = typeof(AnimatorStateMachine).GetMethod("Clear",
		BindingFlags.NonPublic | BindingFlags.Instance);
}
}
#endif