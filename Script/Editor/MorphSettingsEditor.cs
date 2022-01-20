using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using static ShaderMotion.ExpressionPreset;

namespace ShaderMotion {
[CanEditMultipleObjects]
[CustomEditor(typeof(MorphSettings))]
public class MorphSettingsEditor : Editor {
	SerializedProperty exprs;

	Object lastTarget;
	string[] shapeNames;
	bool[] foldout;
	
	int objectPickerId;

	GUIContent iconToolbarPlus;
	GUIContent iconToolbarPlusMore;
	GUIContent iconToolbarMinus;
	const string preButton = "RL FooterButton";
	void OnEnable() {
		iconToolbarPlus = EditorGUIUtility.TrIconContent("Toolbar Plus", "Add to list");
		iconToolbarPlusMore = EditorGUIUtility.TrIconContent("Toolbar Plus More", "Choose to add to list");
		iconToolbarMinus = EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove from list");

		exprs = serializedObject.FindProperty("exprs");

		lastTarget = null;
		shapeNames = new string[0];
		System.Array.Resize(ref foldout, labels.Keys.Max()+1);
	}
	void UpdateSerializedObject(SerializedProperty prop) {
		serializedObject.CopyFromSerializedPropertyIfDifferent(
			new SerializedObject(target).FindProperty(prop.propertyPath));
	}
	public override void OnInspectorGUI() {
		if(lastTarget != target) {
			lastTarget = target;
			shapeNames = MorphSettings.GetShapesInChildren((target as MorphSettings).gameObject,
				smr => smr.name != "Recorder");
		}

		serializedObject.Update();
		if(GUILayout.Button("Auto Detect!")) {
			MorphSettings.AutoDetect(ref (target as MorphSettings).exprs, shapeNames);
			UpdateSerializedObject(exprs);
		}
		foreach(var i in labels.Keys) {
			var expr = exprs.GetArrayElementAtIndex(i);
			var name = expr?.FindPropertyRelative("name");
			var shapes = expr?.FindPropertyRelative("m_Shapes");
			if(name == null)
				continue;

			// label
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PrefixLabel(labels[i], "Button",
				expr.prefabOverride ? EditorStyles.boldLabel : EditorStyles.label);
			var rect = GUILayoutUtility.GetLastRect();
			var newFoldout = shapes.arraySize > 0
				? EditorGUI.Foldout(rect, foldout[i], " ", true)
				: EditorGUI.Foldout(rect, false, " ", true, EditorStyles.label);
			if(shapes.arraySize > 0 || newFoldout)
				foldout[i] = newFoldout;

			// name
			TextOption(EditorGUILayout.GetControlRect(false), name, shapeNames);
			EditorGUILayout.EndHorizontal();

			// shapes
			if(shapes.arraySize == 0 && newFoldout) {
				(target as MorphSettings).exprs[i].shapes = new[]{
					new KeyValuePair<string, float>(name.stringValue, 1f)};
				UpdateSerializedObject(shapes);
			}
			if(foldout[i])
				for(int j=0; j<shapes.arraySize; j++) {
					var shape = shapes.GetArrayElementAtIndex(j);
					var sname = shape?.FindPropertyRelative("name");
					var weight = shape?.FindPropertyRelative("weight");
					if(sname == null || weight == null)
						continue;

					// sname
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.PrefixLabel(" ");
					TextOption(GUILayoutUtility.GetLastRect(), sname, shapeNames);

					// weight
					Slider(EditorGUILayout.GetControlRect(true), weight, 0, 1);

					if(j == 0 && string.IsNullOrEmpty(sname.stringValue)) {
						// import
						if(GUILayout.Button(iconToolbarPlusMore, preButton)) {
							objectPickerId = i;
							EditorGUIUtility.ShowObjectPicker<AnimationClip>(null, false, "",
								EditorGUIUtility.GetControlID(FocusType.Passive));
						}
					// add
					} else if(GUILayout.Button(iconToolbarPlus, preButton))
						shapes.InsertArrayElementAtIndex(j+1);
					// remove
					if(GUILayout.Button(iconToolbarMinus, preButton))
						shapes.DeleteArrayElementAtIndex(j);
					EditorGUILayout.EndHorizontal();
				}
			if(objectPickerId == i && Event.current.commandName == "ObjectSelectorUpdated") {
				var clip = EditorGUIUtility.GetObjectPickerObject() as AnimationClip;
				if(clip) {
					(target as MorphSettings).exprs[i] = ExpressionFromClip(clip);
					UpdateSerializedObject(shapes);
				}
			}
		}
		serializedObject.ApplyModifiedProperties();
	}

	static Expression ExpressionFromClip(AnimationClip clip) {
		var shapes = new Dictionary<string,float>();
		foreach(var binding in AnimationUtility.GetCurveBindings(clip))
			if(binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape.")) {
				var name = binding.propertyName.Substring(11);
				var curve = AnimationUtility.GetEditorCurve(clip, binding);
				shapes[name] = curve[0].value;
			}
		return new Expression{name=clip.name, shapes=shapes.ToArray()};
	}
	static string TextOption(Rect rect, string text, string[] options, bool prefabOverride=false) {
		var style = EditorStyles.popup;
		var fontStyle = style.fontStyle;
		if(prefabOverride)
			style.fontStyle = FontStyle.Bold; 

		var rec2 = Rect.MinMaxRect(rect.xMax - rect.height, rect.yMin, rect.xMax, rect.yMax);
		var idx = EditorGUI.Popup(rec2, -1, options, EditorStyles.label);
		var txt = EditorGUI.DelayedTextField(rect, text, style);

		style.fontStyle = fontStyle;
		return idx != -1 ? options[idx] : txt;
	}
	static void TextOption(Rect rect, SerializedProperty prop, string[] options) {
		EditorGUI.BeginProperty(rect, GUIContent.none, prop);
		EditorGUI.BeginChangeCheck();
		var newValue = TextOption(rect, prop.stringValue, options, prop.prefabOverride);
		if(EditorGUI.EndChangeCheck())
			prop.stringValue = newValue;
		EditorGUI.EndProperty();
	}
	static void Slider(Rect rect, SerializedProperty prop, float leftValue, float rightValue) {
		EditorGUI.BeginProperty(rect, GUIContent.none, prop);
		EditorGUI.BeginChangeCheck();
		var newValue = EditorGUI.Slider(rect, prop.floatValue, leftValue, rightValue);
		if(EditorGUI.EndChangeCheck())
			prop.floatValue = newValue;
		EditorGUI.EndProperty();
	}

	static readonly SortedDictionary<int,string> labels = new SortedDictionary<int,string>{
		{(int)VisemePP, "Viseme PP"},
		{(int)VisemeFF, "Viseme FF"},
		{(int)VisemeTH, "Viseme TH"},
		{(int)VisemeDD, "Viseme DD"},
		{(int)VisemeKK, "Viseme KK"},
		{(int)VisemeCH, "Viseme CH"},
		{(int)VisemeSS, "Viseme SS"},
		{(int)VisemeNN, "Viseme NN"},
		{(int)VisemeRR, "Viseme RR"},
		{(int)VisemeA,  "Viseme A"},
		{(int)VisemeE,  "Viseme E"},
		{(int)VisemeI,  "Viseme I"},
		{(int)VisemeO,  "Viseme O"},
		{(int)VisemeU,  "Viseme U"},
		{(int)BlinkBoth,  "Blink both"},
		{(int)BlinkLeft,  "Blink left"},
		{(int)BlinkRight, "Blink right"},
		{(int)Emotion0, "Emotion 0 (happy)"},
		{(int)Emotion1, "Emotion 1 (excited)"},
		{(int)Emotion2, "Emotion 2 (surprised)"},
		{(int)Emotion3, "Emotion 3 (angry)"},
		{(int)Emotion4, "Emotion 4 (sad)"},
		{(int)Emotion5, "Emotion 5 (bored)"},
		{(int)Emotion6, "Emotion 6 (tired)"},
		{(int)Emotion7, "Emotion 7 (relaxed)"},
	};
}
}