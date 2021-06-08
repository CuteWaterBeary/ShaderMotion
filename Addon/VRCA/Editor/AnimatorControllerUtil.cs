#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace ShaderMotion.Addon {
class AnimatorControllerUtil {
	public static void AddLayerByVal(AnimatorController controller, params AnimatorControllerLayer[] sources) {
		var copier = new StateMachineCopier();
		var layers = controller.layers;
		foreach(var source in sources) {
			var target = layers.FirstOrDefault(x => x.name == source.name);
			if(target == default) {
				controller.AddLayer(source.name);
				layers = controller.layers;
				target = layers.Last();
			}
			// TODO: syncedLayer, override motion/behaviour
			target.avatarMask    = source.avatarMask;
			target.blendingMode  = source.blendingMode;
			target.defaultWeight = source.defaultWeight;
			target.iKPass        = source.iKPass;
			controller.layers = layers;
			copier.CopyMachine(source.stateMachine, target.stateMachine);
		}
	}
	public static void AddParameterByVal(AnimatorController controller, params AnimatorControllerParameter[] sources) {
		var parameters = controller.parameters;
		foreach(var source in sources) {
			var target = parameters.FirstOrDefault(x => x.name == source.name);
			if(target == default) {
				controller.AddParameter(source.name, source.type);
				parameters = controller.parameters;
				target = parameters.Last();
			}
			target.defaultBool  = source.defaultBool;
			target.defaultFloat = source.defaultFloat;
			target.defaultInt   = source.defaultInt;
			target.type         = source.type;
			controller.parameters = parameters;
		}
	}
}
}
#endif