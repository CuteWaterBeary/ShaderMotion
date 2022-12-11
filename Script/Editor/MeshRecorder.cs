#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;
using SortingGroup = UnityEngine.Rendering.SortingGroup;
using System.Text.RegularExpressions;

namespace ShaderMotion {
public class MeshRecorder {
	public static SkinnedMeshRenderer CreateRecorderSkinned(GameObject go, string path, Animator animator, SkinnedMeshRenderer smr=null, bool line=false) {
		var recorder = go.GetComponent<SkinnedMeshRenderer>();
		if(!recorder) {
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));

			var mat = Resources.Load<Material>("MeshRecorder");
			var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Path.ChangeExtension(path, "mesh"));
			if(!mesh) {
				mesh = new Mesh();
				AssetDatabase.CreateAsset(mesh, Path.ChangeExtension(path, "mesh"));
			}

			recorder = go.AddComponent<SkinnedMeshRenderer>();
			recorder.rootBone = recorder.transform;
			recorder.sharedMesh = mesh;
			recorder.sharedMaterial = Resources.Load<Material>("MeshRecorder");
		}
		{
			var mesh = recorder.sharedMesh;
			mesh.Clear(); // avoid polluting blendshapes
			
			var skel = new Skeleton(animator);
			var morph = new Morph(animator);
			var layout = new MotionLayout(skel, morph);
			var gen = new MeshRecorderGen{skel=skel, morph=morph, layout=layout};
			var bones = gen.CreateMeshSkinned(mesh, smr?.sharedMesh, smr?.bones, line:line);

			recorder.bones = bones;
			recorder.sharedMaterials = (smr?.sharedMaterials ?? new Material[0]).Append(
				recorder.sharedMaterials.LastOrDefault()).ToArray();
			recorder.localBounds = recorder.sharedMesh.bounds;
		}
		EditorUtility.SetDirty(recorder);
		AssetDatabase.SaveAssets();
		return recorder;
	}
/*
	public static MeshRenderer CreateRecorderInstanced(GameObject go, string path, Animator animator) {
		var recorder = go.GetComponent<MeshRenderer>();
		if(!recorder) {
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));

			var mat = AssetDatabase.LoadAssetAtPath<Material>(Path.ChangeExtension(path, "mat"));
			if(!mat) {
				mat = Object.Instantiate(Resources.Load<Material>("MeshRecorderInst"));
				AssetDatabase.CreateAsset(mat, Path.ChangeExtension(path, "mat"));
			}
			var mesh = Resources.Load<Mesh>("MeshRecorderInst");
			if(!mesh) {
				mesh = new Mesh();
				mesh.name = "quad";
				MeshRecorderGen.CreateMeshInstanced(mesh);
				AssetDatabase.AddObjectToAsset(mesh, Resources.Load<Material>("MeshRecorderInst"));
			}

			recorder = go.AddComponent<MeshRenderer>();
			recorder.sharedMaterial = mat;
			recorder.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
			// sorting order
			recorder.sortingOrder = -1;
			if(!animator.GetComponent<SortingGroup>())
				animator.gameObject.AddComponent<SortingGroup>();
			Debug.Assert(recorder.transform.IsChildOf(animator.transform), "recorder should be child of animator");
		}
		{
			var mat = recorder.sharedMaterial;
			var mesh = recorder.GetComponent<MeshFilter>().sharedMesh;
			var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.ChangeExtension(path, "texture2D"));
			if(!tex) {
				tex = new Texture2D(1,1);
				AssetDatabase.CreateAsset(tex, Path.ChangeExtension(path, "texture2D"));
			}
			mat.mainTexture = tex;

			var skel = new Skeleton(animator);
			var morph = new Morph(animator);
			var layout = new MotionLayout(skel, morph);
			var gen = new MeshRecorderGen{skel=skel, morph=morph, layout=layout};
			gen.CreateTexture(tex);
			
			for(int b=0; b<skel.bones.Length; b++) {
				var bone = skel.bones[b] && !skel.dummy[b] ? skel.bones[b] : recorder.transform;
				var renderer = (bone.Find($"${b}") ?? new GameObject($"${b}",
					typeof(MeshFilter), typeof(MeshRenderer)).transform).GetComponent<MeshRenderer>();
				renderer.transform.SetParent(bone, false);
				renderer.transform.gameObject.layer = recorder.gameObject.layer; // copy layer
				renderer.sharedMaterial = mat;
				renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
				renderer.sortingOrder = recorder.sortingOrder + b+1;
			}
		}
		EditorUtility.SetDirty(recorder);
		AssetDatabase.SaveAssets();
		return recorder;
	}
*/
	public static GameObject CreateChild(Component parent, string name, Transform transform=null) {
		var go = parent ? parent.transform.Find(name)?.gameObject : GameObject.Find("/"+name);
		if(!go) {
			go = new GameObject(name);
			go.transform.SetParent(transform ?? parent.transform, false);
			go.transform.SetParent(parent?.transform, true);
		}
		return go;
	}
	public static string CreatePath(Animator animator, string name) {
		var path = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)), "auto",
			$"{Regex.Replace(animator.avatar.name, @"Avatar$", "", RegexOptions.None)}{name}.asset");
		return path.StartsWith("Assets") ? path : Path.Combine("Assets", path);
	}

	public static SkinnedMeshRenderer CreateRecorderSkinned(Animator animator, SkinnedMeshRenderer smr=null, bool line=false) {
		return CreateRecorderSkinned(CreateChild(animator, "Recorder"), CreatePath(animator, "Recorder"),
			animator, smr, line:line);
	}
/*
	public static MeshRenderer CreateRecorderInstanced(Animator animator) {
		return CreateRecorderInstanced(CreateChild(animator, "Recorder"), CreatePath(animator, "Recorder"),
			animator);
	}
*/
	[MenuItem("CONTEXT/SkinnedMeshRenderer/CreateMeshRecorder")]
	static void CreateRecorderSkinned_FromSkinnedMeshRenderer(MenuCommand command) {
		var smr = (SkinnedMeshRenderer)command.context;
		var animator = smr.gameObject.GetComponentInParent<Animator>();
		EditorGUIUtility.PingObject(CreateRecorderSkinned(animator, smr:smr));
	}
	[MenuItem("CONTEXT/Animator/CreateMeshRecorder")]
	static void CreateRecorderSkinned_FromAnimator(MenuCommand command) {
		EditorGUIUtility.PingObject(CreateRecorderSkinned((Animator)command.context));
	}
	[MenuItem("CONTEXT/Animator/CreateMeshRecorder (Outline)")]
	static void CreateRecorderSkinned_FromAnimatorWithLine(MenuCommand command) {
		EditorGUIUtility.PingObject(CreateRecorderSkinned((Animator)command.context, line:true));
	}
/*
	[MenuItem("CONTEXT/Animator/CreateMeshRecorder (Instanced)")]
	static void CreateRecorderInstanced_FromAnimator(MenuCommand command) {
		EditorGUIUtility.PingObject(CreateRecorderInstanced((Animator)command.context));
	}
*/
}
}
#endif