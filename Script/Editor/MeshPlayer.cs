#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;

namespace ShaderMotion {
public class MeshPlayer {
	public static MeshRenderer CreatePlayer(GameObject go, string path, Animator animator, Renderer[] renderers) {
		var player = go.GetComponent<MeshRenderer>();
		if(!player) {
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));

			var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Path.ChangeExtension(path, "mesh"));
			if(!mesh) {
				mesh = new Mesh();
				AssetDatabase.CreateAsset(mesh, Path.ChangeExtension(path, "mesh"));
			}
			var mats = new List<Material>();
			foreach(var renderer in renderers)
			foreach(var srcMat in renderer.sharedMaterials) {
				var matPath = Path.ChangeExtension(path, mats.Count == 0 ? "mat" : $"{mats.Count}.mat");
				var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
				if(!mat) {
					mat = Object.Instantiate(Resources.Load<Material>("MeshPlayer"));
					AssetDatabase.CreateAsset(mat, matPath);
				}
				if(srcMat.HasProperty("_MainTex"))
					mat.mainTexture = srcMat.mainTexture;
				if(srcMat.HasProperty("_Color"))
					mat.color = srcMat.color;
				mats.Add(mat);
				if(!(renderer is SkinnedMeshRenderer || renderer is MeshRenderer))
					break; // only one material since it's treated as a quad
			}

			player = go.AddComponent<MeshRenderer>();
			player.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
			player.sharedMaterials = mats.ToArray();
			CopySettings(renderers[0], player);
		}
		{
			var mesh = player.GetComponent<MeshFilter>().sharedMesh;
			var texs = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(mesh))
							.Where(a=>a is Texture2D).ToDictionary(a=>a.name, a=>(Texture2D)a);
			var texNames = new HashSet<string>{"Bone", "Shape"};
			foreach(var texName in texNames) {
				if(!texs.ContainsKey(texName)) {
					var tex = new Texture2D(1,1);
					tex.name = texName;
					texs[texName] = tex;
					AssetDatabase.AddObjectToAsset(tex, mesh);
				}
				foreach(var mat in player.sharedMaterials) {
					mat.shader = Resources.Load<Material>("MeshPlayer").shader;
					mat.SetTexture($"_{texName}", texs[texName]);
				}
			}
			foreach(var tex in texs.Values.Where(t => !texNames.Contains(t.name)).ToArray())
				Object.DestroyImmediate(tex, true);

			var skel = new Skeleton(animator);
			var morph = new Morph(animator);
			var layout = new MotionLayout(skel, morph);
			var gen = new MeshPlayerGen{skel=skel, morph=morph, layout=layout};
			var sources = new (Mesh, Transform[])[renderers.Length];
			for(int i=0; i<renderers.Length; i++)
				switch(renderers[i]) {
				case SkinnedMeshRenderer smr:
					sources[i] = (smr.sharedMesh, smr.bones); break;
				case MeshRenderer mr:
					sources[i] = (mr.GetComponent<MeshFilter>().sharedMesh, new[]{mr.transform}); break;
				default: // treated as a quad
					sources[i] = (Resources.GetBuiltinResource<Mesh>("Quad.fbx"), new[]{renderers[i].transform}); break;
				}
			gen.CreatePlayer(mesh, texs["Bone"], texs["Shape"], sources);

			// make bounds rotational invariant and extend by motion radius
			const float motionRadius = 4;
			var size = mesh.bounds.size;
			var sizeXZ = Mathf.Max(size.x, size.z) + 2*motionRadius;
			mesh.bounds = new Bounds(mesh.bounds.center, new Vector3(sizeXZ, size.y, sizeXZ));
			MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Low);
		}
		EditorUtility.SetDirty(player);
		AssetDatabase.SaveAssets();
		return player;
	}
	static void CopySettings(Renderer src, Renderer dst) {
		dst.lightProbeUsage				= src.lightProbeUsage;
		dst.reflectionProbeUsage		= src.reflectionProbeUsage;
		dst.shadowCastingMode			= src.shadowCastingMode;
		dst.receiveShadows 				= src.receiveShadows;
		dst.motionVectorGenerationMode	= src.motionVectorGenerationMode;
		dst.allowOcclusionWhenDynamic	= src.allowOcclusionWhenDynamic;
	}

	public static MeshRenderer CreatePlayer(Animator animator, Renderer[] renderers=null) {
		return CreatePlayer(
			MeshRecorder.CreateChild(animator.transform.parent, $"{animator.name}.Player", animator.transform),
			MeshRecorder.CreatePath(animator, "Player"),
			animator, renderers ?? animator.gameObject.GetComponentsInChildren<Renderer>()
				.Where(smr => !smr.name.StartsWith("Recorder")).ToArray());
	}

	[MenuItem("CONTEXT/SkinnedMeshRenderer/CreateMeshPlayer")]
	static void CreatePlayer_FromSkinnedMeshRenderer(MenuCommand command) {
		var smr = (SkinnedMeshRenderer)command.context;
		var animator = smr.gameObject.GetComponentInParent<Animator>();
		// combine all selected SMRs in the same avatar
		var smrs = Selection.gameObjects.Select(x => x.GetComponent<SkinnedMeshRenderer>())
					.Where(x => (bool)x && x.gameObject.GetComponentInParent<Animator>() == animator).ToArray();
		if(smrs.Length > 0 && smrs[0] == smr) // avoid multiple execution on multi-select
			EditorGUIUtility.PingObject(CreatePlayer(animator, smrs));
	}
	[MenuItem("CONTEXT/Animator/CreateMeshPlayer")]
	static void CreatePlayer_FromAnimator(MenuCommand command) {
		EditorGUIUtility.PingObject(CreatePlayer((Animator)command.context));
	}
}
}
#endif