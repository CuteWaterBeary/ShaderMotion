using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using static ShaderMotion.BlendSpacePreset;
using static ShaderMotion.ExpressionPreset;

namespace ShaderMotion {
[System.Serializable]
public struct Expression : ISerializationCallbackReceiver {
	public string name;
	[System.NonSerialized]
	public KeyValuePair<string,float>[] shapes;
	public Expression(string name, params string[] shapes) : this() {
		this.name = name;
		this.shapes = System.Array.ConvertAll(shapes, x => new KeyValuePair<string,float>(x, 1f));
	}

	[System.Serializable]
	private struct Shape {
		public string name;
		public float weight;
	}
	[SerializeField]
	private Shape[] m_Shapes;
	public void OnBeforeSerialize() {
		m_Shapes = shapes == null ? null : System.Array.ConvertAll(shapes,
			x => new Shape{name=x.Key, weight=x.Value});
	}
	public void OnAfterDeserialize() {
		shapes = m_Shapes == null ? null : System.Array.ConvertAll(m_Shapes,
			x => new KeyValuePair<string,float>(x.name, x.weight));
	}
}
public class MorphSettings : MonoBehaviour  {
	public Expression[] exprs = new Expression[exprPresets.Length];
	public static void AutoDetect(ref Expression[] exprs, IEnumerable<string> shapeNames) {
		System.Array.Resize(ref exprs, exprPresets.Length);
		for(int i=0; i<exprPresets.Length; i++)
			if(string.IsNullOrEmpty(exprs[i].name) && exprPresets[i].shapes != null)
				exprs[i].name = exprPresets[i].shapes[0].Key.Split('#')
					.SelectMany(p => shapeNames.Where((System.Func<string, bool>)
						new Regex(p, RegexOptions.Compiled|RegexOptions.IgnoreCase).IsMatch)).FirstOrDefault();
	}
	public static void Apply(Morph morph, Animator animator) {
		var settings = animator.GetComponent<MorphSettings>();
		var exprs = default(Expression[]);
		if(settings)
			exprs = (Expression[])settings.exprs.Clone();
		else
			AutoDetect(ref exprs, GetShapesInChildren(animator.gameObject));

		for(int e=0; e<exprs.Length; e++) {
			if(exprs[e].shapes?.Length == 0)
				exprs[e].shapes = null;
			if(string.IsNullOrEmpty(exprs[e].name))
				exprs[e].name = exprPresets[e].name;
			else if(exprs[e].shapes == null)
				exprs[e].shapes = new[]{new KeyValuePair<string,float>(exprs[e].name, 1f)};
		}

		foreach(var (b, name, controls) in blendPresets) {
			var blend = new BlendSpace{name=name};
			for(int i=0; i<controls.Length; i++)
				foreach(var (e, coord) in controls[i]) {
					morph.controls[exprs[(int)e].name] = ((int)b, coord);
					if(i == 0)
						blend.Set(coord, exprs[(int)e].shapes);
				}
			morph.blends[(int)b] = blend;
		}
	}
	public static string[] GetShapesInChildren(GameObject root) {
		return root.GetComponentsInChildren<SkinnedMeshRenderer>()
			.Select(smr => smr.sharedMesh)
			.SelectMany(mesh => Enumerable.Range(0, mesh?mesh.blendShapeCount:0)
				.Select(i => mesh.GetBlendShapeName(i))).Distinct().ToArray();
	}

	static readonly string visemePattern = @"[.]v_({0})$#v_({0})$#^({0})$#[^0-9a-z]({0})$";
	static readonly string emotionPattern = @"^({0})$";
	static readonly Expression[] exprPresets = Enumerable.Range(0, 1+System.Enum.GetValues(typeof(ExpressionPreset)).Cast<int>().Last())
		.ToDictionary(i => (ExpressionPreset)i, i => default(Expression))
		.Concat(new Dictionary<ExpressionPreset,Expression>
	{
		// https://developer.oculus.com/documentation/unity/audio-ovrlipsync-viseme-reference
		{VisemePP, new Expression("v_pp", string.Format(visemePattern, "pp"))},
		{VisemeFF, new Expression("v_ff", string.Format(visemePattern, "ff"))},
		{VisemeTH, new Expression("v_th", string.Format(visemePattern, "th"))},
		{VisemeDD, new Expression("v_dd", string.Format(visemePattern, "dd"))},
		{VisemeKK, new Expression("v_kk", string.Format(visemePattern, "kk"))},
		{VisemeCH, new Expression("v_ch", string.Format(visemePattern, "ch"))},
		{VisemeSS, new Expression("v_ss", string.Format(visemePattern, "ss"))},
		{VisemeNN, new Expression("v_nn", string.Format(visemePattern, "nn"))},
		{VisemeRR, new Expression("v_rr", string.Format(visemePattern, "rr"))},
		{VisemeA,  new Expression("v_aa", string.Format(visemePattern, "a|aa|ah"))}, // EN: [ɑ], JP: [ä]
		{VisemeE,  new Expression("v_e",  string.Format(visemePattern, "e|eh"))}, // EN: [ɛ], JP: [e̞]
		{VisemeI,  new Expression("v_ih", string.Format(visemePattern, "i|ih"))}, // EN: [ɪ], JP: [i]
		{VisemeO,  new Expression("v_oh", string.Format(visemePattern, "o|oh"))}, // EN: [o], JP: [o̞]
		{VisemeU,  new Expression("v_ou", string.Format(visemePattern, "u|ou"))}, // EN: [ʊ], JP: [ɯ]
		// https://github.com/vrm-c/vrm-specification/pull/175
		// https://en.wikipedia.org/wiki/Emotion_classification#Circumplex_model
		{Emotion0, new Expression("emotion_0", string.Format(emotionPattern, "(mood_)?happy|joy"))}, // VRC,VRM
		{Emotion1, new Expression("emotion_1", string.Format(emotionPattern, "excited"))},
		{Emotion2, new Expression("emotion_2", string.Format(emotionPattern, "(mood_)?surprised|surprise"))}, // VRC,VRM
		{Emotion3, new Expression("emotion_3", string.Format(emotionPattern, "(mood_)?angry|anger"))}, // VRC,VRM
		{Emotion4, new Expression("emotion_4", string.Format(emotionPattern, "(mood_)?sad|sorrow"))}, // VRC,VRM
		{Emotion5, new Expression("emotion_5", string.Format(emotionPattern, "bored"))},
		{Emotion6, new Expression("emotion_6", string.Format(emotionPattern, "tired"))},
		{Emotion7, new Expression("emotion_7", string.Format(emotionPattern, "relaxed|fun"))}, // VRM
	}).GroupBy(g => g.Key, (k,g) => g.Last().Value).ToArray();

	static readonly (BlendSpacePreset, string, (ExpressionPreset, Vector2)[][])[] blendPresets = new[]{
		(LipSync, "lipsync", new[]{new[]{
			// based on https://en.wikipedia.org/wiki/Vowel#Height#Roundedness
			(VisemeA,  new Vector2( 1f,  0f)), // open vowel
			(VisemeI,  new Vector2( 0f, +1f)), // close unrounded vowel
			(VisemeU,  new Vector2( 0f, -1f)), // close rounded vowel
			(VisemeE,  new Vector2(.5f, +1f)), // mid unrounded vowel
			(VisemeO,  new Vector2(.5f, -1f)), // mid rounded vowel
		}, new[]{
			// based on https://developer.oculus.com/documentation/unity/audio-ovrlipsync-viseme-reference/#reference-images
			(VisemePP, new Vector2( 0f,  0f)),
			(VisemeFF, new Vector2( 0f, -1f)), // U
			(VisemeTH, new Vector2(.5f, +1f)), // E
			(VisemeDD, new Vector2( 0f, +1f)), // I
			(VisemeKK, new Vector2( 0f, +1f)), // I
			(VisemeCH, new Vector2( 0f, +1f)), // I
			(VisemeSS, new Vector2( 0f,+.8f)), // I * 0.8
			(VisemeNN, new Vector2( 0f, +1f)), // I
			(VisemeRR, new Vector2( 0f, -1f)), // U
		}}),
		(Emotion, "emotion", new[]{new[]{
			// counter-clockwise circumplex
			(Emotion0, new Vector2(+1f,  0f)),
			(Emotion1, new Vector2(+1f, +1f)),
			(Emotion2, new Vector2( 0f, +1f)),
			(Emotion3, new Vector2(-1f, +1f)),
			(Emotion4, new Vector2(-1f,  0f)),
			(Emotion5, new Vector2(-1f, -1f)),
			(Emotion6, new Vector2( 0f, -1f)),
			(Emotion7, new Vector2( 1f, -1f)),
		}}),
	};
}
public enum ExpressionPreset {
	VisemePP = 1, // consistent with OVRLipSync.Viseme
	VisemeFF,
	VisemeTH,
	VisemeDD,
	VisemeKK,
	VisemeCH,
	VisemeSS,
	VisemeNN,
	VisemeRR,
	VisemeA,
	VisemeE,
	VisemeI,
	VisemeO,
	VisemeU,

	Emotion0 = 15,
	Emotion1,
	Emotion2,
	Emotion3,
	Emotion4,
	Emotion5,
	Emotion6,
	Emotion7,
}
}