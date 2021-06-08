#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Reflection;

namespace ShaderMotion.Addon {
class StateMachineCopier {
	static readonly MethodInfo Clear = typeof(AnimatorStateMachine).GetMethod("Clear", BindingFlags.NonPublic | BindingFlags.Instance);
	Dictionary<AnimatorStateMachine, AnimatorStateMachine> machineMap = new Dictionary<AnimatorStateMachine, AnimatorStateMachine>();
	Dictionary<AnimatorState, AnimatorState> stateMap = new Dictionary<AnimatorState, AnimatorState>();

	public void CopyMachine(AnimatorStateMachine source, AnimatorStateMachine target) {
		Clear.Invoke(target, new object[]{});

		stateMap.Clear();
		machineMap.Clear();
		machineMap[source] = target;
		CreateSubMachineAndStates(source, target);

		foreach(var statePair in stateMap)
			CopySerialized_ConvTrans(statePair.Key, statePair.Value);
		foreach(var machinePair in machineMap)
			CopySerialized_KeepChild_ConvTrans(machinePair.Key, machinePair.Value);
	}
	void CreateSubMachineAndStates(AnimatorStateMachine source, AnimatorStateMachine target) {
		foreach(var child in source.states)
			if(stateMap.TryGetValue(child.state, out var targetState))
				target.AddState(targetState, child.position);
			else
				stateMap[child.state] = target.AddState(child.state.name, child.position);
		foreach(var child in source.stateMachines)
			if(machineMap.TryGetValue(child.stateMachine, out var targetMachine))
				target.AddStateMachine(targetMachine, child.position);
			else
				CreateSubMachineAndStates(child.stateMachine, machineMap[child.stateMachine] =
					target.AddStateMachine(child.stateMachine.name, child.position));
	}
	void CopySerialized_KeepChild_ConvTrans(AnimatorStateMachine source, AnimatorStateMachine target) {
		var states = target.states;
		var stateMachines = target.stateMachines;
		EditorUtility.CopySerialized(source, target); // NOTE: behaviours are shared
		target.states = states;
		target.stateMachines = stateMachines;

		// convert transitions
		target.defaultState = source.defaultState ? stateMap[source.defaultState] : null;
		target.anyStateTransitions = new AnimatorStateTransition[0];
		target.entryTransitions = new AnimatorTransition[0];
		foreach(var trans in source.anyStateTransitions)
			CopySerialized_KeepDest(trans, trans.destinationStateMachine ?
				target.AddAnyStateTransition(machineMap[trans.destinationStateMachine]) :
				target.AddAnyStateTransition(stateMap[trans.destinationState]));
		foreach(var trans in source.entryTransitions)
			CopySerialized_KeepDest(trans, trans.destinationStateMachine ?
				target.AddEntryTransition(machineMap[trans.destinationStateMachine]) :
				target.AddEntryTransition(stateMap[trans.destinationState]));

		// NOTE: state machine transitions seem automatically cleared
		foreach(var child in source.stateMachines) {
			var c = machineMap[child.stateMachine];
			foreach(var trans in source.GetStateMachineTransitions(child.stateMachine))
				CopySerialized_KeepDest(trans, trans.isExit ? target.AddStateMachineExitTransition(c) :
					trans.destinationStateMachine ?
					target.AddStateMachineTransition(c, machineMap[trans.destinationStateMachine]) :
					target.AddStateMachineTransition(c, stateMap[trans.destinationState]));
		}
	}
	void CopySerialized_ConvTrans(AnimatorState source, AnimatorState target) {
		EditorUtility.CopySerialized(source, target); // NOTE: motion & behaviours are shared

		// convert transitions
		target.transitions = new AnimatorStateTransition[0];
		foreach(var trans in source.transitions)
			CopySerialized_KeepDest(trans, trans.isExit ? target.AddExitTransition() :
				trans.destinationStateMachine ?
				target.AddTransition(machineMap[trans.destinationStateMachine]) :
				target.AddTransition(stateMap[trans.destinationState]));
	}
	static void CopySerialized_KeepDest(AnimatorTransitionBase source, AnimatorTransitionBase target) {
		var destinationState = target.destinationState;
		var destinationStateMachine = target.destinationStateMachine;
		EditorUtility.CopySerialized(source, target);
		target.destinationState = destinationState;
		target.destinationStateMachine = destinationStateMachine;
	}

	// utility menu
	static AnimatorStateMachine menuMachine;
	[MenuItem("CONTEXT/AnimatorStateMachine/Copy StateMachine")]
	static void MenuCopy(MenuCommand command) {
		menuMachine = (AnimatorStateMachine)command.context;
	}
	[MenuItem("CONTEXT/AnimatorStateMachine/Paste StateMachine Values")]
	static void MenuPaste(MenuCommand command) {
		if(menuMachine)
			new StateMachineCopier().CopyMachine(menuMachine, (AnimatorStateMachine)command.context);
	}
}
}
#endif