#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;

namespace ShaderMotion {
public class MeshPlayer {
	public static void CreatePlayerTex(Texture2D tex, Skeleton skel, MotionLayout layout) {
		var boneData = new float[skel.bones.Length, 2];
		var ancestors = new List<int>[skel.bones.Length];
		for(int b=0; b<skel.bones.Length; b++) {
			var idx0 = layout.bones[b].Select((idx, axis) => idx < 0 ? 0 : idx - (axis - (axis<3?0:3))).Max();
			var mask = layout.bones[b].Select((idx, axis) => idx < 0 ? 0 : 1<<axis).Sum();
			boneData[b, 0] = idx0;
			boneData[b, 1] = (mask & 7) + (skel.axes[b].sign > 0 ? 0 : 8);

			ancestors[b] = new List<int>();
			for(int p=b; p>=0; p=skel.parents[p])
				ancestors[b].Add(p);
			ancestors[b].Reverse();
		}

		tex.Resize(ancestors.Max(l=>l.Count)*2+2, skel.bones.Length, TextureFormat.RGBAFloat, false);
		var colors = Enumerable.Repeat(new Color(0,0,0,-1), tex.width * tex.height).ToArray();
		for(int i=0; i<skel.bones.Length; i++) if(skel.bones[i])
			for(int j=0; j<ancestors[i].Count; j++) {
				var b = ancestors[i][j];
				var p = skel.parents[b];
				Vector3 pos;
				Quaternion rot;
				if(p < 0) {
					rot = Quaternion.identity;
					pos = new Vector3(0, skel.humanScale, 0);
				} else if(skel.bones[p] == skel.bones[b]) {
					rot = Quaternion.Inverse(skel.axes[p].preQ) * skel.axes[b].preQ;
					pos = Vector3.zero;
				} else {
					var inv = Quaternion.Inverse(skel.bones[p].rotation * skel.axes[p].postQ);
					rot = inv * (skel.bones[b].parent.rotation * skel.axes[b].preQ);
					pos = (inv * skel.root.rotation) * skel.root.InverseTransformVector(
														skel.bones[b].position - skel.bones[p].position);
				}
				var deg = rot.eulerAngles * Mathf.Deg2Rad;
				colors[i*tex.width + j*2 + 0] = new Color(pos.x, pos.y, pos.z, boneData[b, 0]);
				colors[i*tex.width + j*2 + 1] = new Color(deg.x, deg.y, deg.z, boneData[b, 1]);
			}
		tex.SetPixels(colors);
		tex.Apply(false, false);

		// enforce nearest sampler
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.filterMode = FilterMode.Point;
		tex.anisoLevel = 0;
	}
	public static void CreatePlayerMesh(Mesh mesh, Skeleton skel, Appearance appr, MotionLayout layout, (Mesh,Transform[])[] sources, float motionRadius=2) {
		const int quality=2, shapeQuality=4;

		var vertices = new List<Vector3>();
		var uvShape  = new List<List<Vector4>>();
		var uvSkin   = Array.ConvertAll(new int[quality], i=>Array.ConvertAll(new int[3], j=>new List<Vector4>()));
		var bindPreM = skel.bones.Select((b,i) => !b ? Matrix4x4.identity :
			Matrix4x4.Rotate(Quaternion.Inverse(b.rotation * skel.axes[i].postQ) * skel.root.rotation)
				 * (skel.root.worldToLocalMatrix * b.localToWorldMatrix)).ToArray();
		foreach(var (srcMesh, srcBones) in sources) {
			var srcVertices = srcMesh.vertices;
			var srcNormals  = srcMesh.normals;
			var srcTangents = srcMesh.tangents;
			var srcUVs      = srcMesh.uv;
			MeshUtil.FixNormalTangents(srcMesh, ref srcNormals, ref srcTangents);
			if(vertices != null)
				vertices.AddRange(srcVertices.Select(
					(skel.root.worldToLocalMatrix * srcBones[0].localToWorldMatrix * srcMesh.bindposes[0]).MultiplyPoint3x4));

			var boneBinds = MeshUtil.RetargetWeightBindposes(
				srcMesh.boneWeights, srcMesh.bindposes, srcBones, skel.bones, (int)HumanBodyBones.Hips, quality:quality);
			for(int v=0; v<srcMesh.vertexCount; v++)
				for(int i=0; i<quality; i++) {
					var (bone, bind) = boneBinds[v, i];
					var vert = bindPreM[bone].MultiplyVector(bind.MultiplyPoint3x4(srcVertices[v]));
					var norm = bindPreM[bone].MultiplyVector(bind.MultiplyVector  (srcNormals[v]));
					var tang = bindPreM[bone].MultiplyVector(bind.MultiplyVector  (srcTangents[v]));
					uvSkin[i][0].Add(new Vector4(vert.x, vert.y, vert.z, bind[3,3]/2 + bone));
					uvSkin[i][1].Add(new Vector4(norm.x, norm.y, norm.z, srcUVs[v][i]));
					uvSkin[i][2].Add(new Vector4(tang.x, tang.y, tang.z, srcTangents[v].w));
				}

			uvShape.AddRange(Enumerable.Repeat<List<Vector4>>(null, srcMesh.vertexCount));
			var srcDV = new Vector3[srcMesh.vertexCount];
			var dstDV = new Vector3[srcMesh.vertexCount];
			var shapePreM = srcNormals.Select((norm, v) => new Matrix4x4(norm, (Vector3)srcTangents[v],
				Vector3.Cross(norm.normalized, srcTangents[v]), new Vector4(0,0,0,1)).inverse).ToArray();
			for(int e=0; e<appr.exprShapes.Length; e++) {
				Array.Clear(dstDV, 0, dstDV.Length);
				foreach(var s in appr.exprShapes[e]) {
					var shape = srcMesh.GetBlendShapeIndex(s.Key);
					if(shape >= 0) {
						srcMesh.GetBlendShapeFrameVertices(shape,
							srcMesh.GetBlendShapeFrameCount(shape)-1, srcDV, null, null);
						for(int v=0; v<srcMesh.vertexCount; v++)
							dstDV[v] += srcDV[v] * s.Value;
					}
				}
				for(int v=0; v<srcMesh.vertexCount; v++)
					if(dstDV[v] != Vector3.zero) {
						var dv = shapePreM[v].MultiplyVector(dstDV[v]);
						var k = uvShape.Count - srcMesh.vertexCount + v;
						uvShape[k] = uvShape[k] ?? new List<Vector4>();
						uvShape[k].Add(new Vector4(dv.x, dv.y, dv.z, layout.exprs[e]));
					}
			}
		}

		mesh.Clear();
		mesh.SetVertices(vertices);
		mesh.SetNormals(uvSkin[0][2].ConvertAll(x => (Vector3)x));
		mesh.SetTangents(uvSkin[1][2]);
		for(int i=0; i<quality; i++)
			for(int j=0; j<2; j++)
				mesh.SetUVs(i*2+j, uvSkin[i][j]);
		for(int i=0; i<shapeQuality; i++)
			mesh.SetUVs(4+i, uvShape.ConvertAll(x => i < (x?.Count??0) ? x[i] : Vector4.zero));

		int vertexOffset = 0, subMeshIndex = 0;
		mesh.indexFormat  = sources.Max(s => s.Item1.indexFormat);
		mesh.subMeshCount = sources.Sum(s => s.Item1.subMeshCount);
		foreach(var (srcMesh, srcBones) in sources) {
			for(int i=0; i<srcMesh.subMeshCount; i++)
				mesh.SetIndices(srcMesh.GetIndices(i, false), srcMesh.GetTopology(i),
								subMeshIndex++, false, vertexOffset + (int)srcMesh.GetBaseVertex(i));
			vertexOffset += srcMesh.vertexCount;
		}

		mesh.RecalculateBounds();
		// make bounds rotational invariant and extend by motion radius
		var size = mesh.bounds.size;
		var sizeXZ = Mathf.Max(size.x, size.z) + 2*motionRadius;
		mesh.bounds = new Bounds(mesh.bounds.center, new Vector3(sizeXZ, size.y, sizeXZ));
	}
	public static MeshRenderer CreatePlayer(string name, Transform parent, Animator animator, SkinnedMeshRenderer[] smrs, string path) {
		var player = (parent ? parent.Find(name) : GameObject.Find("/"+name)?.transform)?.GetComponent<MeshRenderer>();
		if(!player) {
			if(!System.IO.Directory.Exists(Path.GetDirectoryName(path)))
				System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));

			var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
			if(!mesh) {
				mesh = new Mesh();
				AssetDatabase.CreateAsset(mesh, path);
			}
			var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
			if(!tex) {
				tex = new Texture2D(1,1);
				tex.name = "Armature";
				AssetDatabase.AddObjectToAsset(tex, mesh);
			}
			var mats = new List<Material>();
			foreach(var smr in smrs)
				foreach(var srcMat in smr.sharedMaterials) {
					var matPath = Path.ChangeExtension(path, mats.Count == 0 ? "mat" : $"{mats.Count}.mat");
					var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
					if(!mat) {
						mat = Object.Instantiate(Resources.Load<Material>("MeshPlayer"));
						AssetDatabase.CreateAsset(mat, matPath);
					}
					if(srcMat.HasProperty("_Color"))
						mat.color = srcMat.color;
					mat.mainTexture = srcMat.mainTexture;
					mat.SetTexture("_Armature", tex);
					mats.Add(mat);
				}

			var go = new GameObject(name, typeof(MeshRenderer), typeof(MeshFilter));
			go.transform.SetParent(parent);
			go.transform.SetPositionAndRotation(animator.transform.position, animator.transform.rotation);
			player = go.GetComponent<MeshRenderer>();
			player.GetComponent<MeshFilter>().sharedMesh = mesh;
			player.sharedMaterials = mats.ToArray();
			// copy renderer settings
			player.lightProbeUsage				= smrs[0].lightProbeUsage;
			player.reflectionProbeUsage			= smrs[0].reflectionProbeUsage;
			player.shadowCastingMode			= smrs[0].shadowCastingMode;
			player.receiveShadows 				= smrs[0].receiveShadows;
			player.motionVectorGenerationMode	= smrs[0].motionVectorGenerationMode;
			player.allowOcclusionWhenDynamic	= smrs[0].allowOcclusionWhenDynamic;
		}
		{
			var skeleton = new Skeleton(animator);
			var appr = new Appearance(smrs[0].sharedMesh, true);
			var layout = new MotionLayout(skeleton, MotionLayout.defaultHumanLayout,
											appr, MotionLayout.defaultExprLayout);

			var tex = (Texture2D)player.sharedMaterial.GetTexture("_Armature");
			var mesh = player.GetComponent<MeshFilter>().sharedMesh;
			CreatePlayerTex(tex, skeleton, layout);
			CreatePlayerMesh(mesh, skeleton, appr, layout,
				smrs.Select(smr => (smr.sharedMesh, smr.bones)).ToArray(), motionRadius:4);
			EditorUtility.SetDirty(player);
			AssetDatabase.SaveAssets();
		}
		return player;
	}
	public static string CreatePlayerPath(Animator animator) {
		var name = animator.avatar.name;
		var path = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)), "auto",
			(name.EndsWith("Avatar") ? name.Substring(0, name.Length-6) : name)
			+ "Player.mesh");
		return path.StartsWith("Assets") ? path : Path.Combine("Assets", path);
	}
}
class MeshPlayerEditor {
	static void CreatePlayer(Animator animator, SkinnedMeshRenderer[] smrs) {
		EditorGUIUtility.PingObject(MeshPlayer.CreatePlayer(animator.name + ".Player",
			animator.transform.parent, animator, smrs, MeshPlayer.CreatePlayerPath(animator)));
	}
	[MenuItem("CONTEXT/SkinnedMeshRenderer/CreateMeshPlayer")]
	static void CreatePlayerFromSkinnedMeshRenderer(MenuCommand command) {
		var smr = (SkinnedMeshRenderer)command.context;
		var animator = smr.gameObject.GetComponentInParent<Animator>();
		// combine all SMRs in the same avatar
		var smrs = Selection.gameObjects.Select(x => x.GetComponent<SkinnedMeshRenderer>())
					.Where(x => (bool)x && x.gameObject.GetComponentInParent<Animator>() == animator).ToArray();
		if(smrs.Length > 0 && smrs[0] == smr)
			CreatePlayer(animator, smrs);
	}
	[MenuItem("CONTEXT/Animator/CreateMeshPlayer")]
	static void CreatePlayerFromAnimator(MenuCommand command) {
		var animator = (Animator)command.context;
		var smrs = animator.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
		if(smrs.Length > 0)
			CreatePlayer(animator, smrs);
	}
}
}
#endif