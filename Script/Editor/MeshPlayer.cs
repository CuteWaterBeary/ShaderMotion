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
		for(int i=0; i<skel.bones.Length; i++) {
			var slot = layout.baseIndices[i] - (layout.channels[i][0] - (layout.channels[i][0] < 3 ? 0 : 3));
			var mask = layout.channels[i].Sum(j => 1<<j);
			boneData[i, 0] = slot;
			boneData[i, 1] = (mask & 7) + (skel.axes[i].sign > 0 ? 0 : 8);

			ancestors[i] = new List<int>();
			for(int b=i; b>=0; b=skel.parents[b])
				ancestors[i].Add(b);
			ancestors[i].Reverse();
		}

		tex.Resize(ancestors.Max(l=>l.Count)*2+2, skel.bones.Length, TextureFormat.RGBAFloat, false);
		var colors = Enumerable.Repeat(new Color(0,0,0,-1), tex.width * tex.height).ToArray();
		for(int i=0; i<skel.bones.Length; i++) if(skel.bones[i])
			for(int j=0; j<ancestors[i].Count; j++) {
				var b = ancestors[i][j];
				var p = skel.parents[b];
				Vector3 pos, rot;
				if(p < 0) {
					rot = Vector3.zero;
					pos = new Vector3(0, skel.scale, 0);
				} else {
					rot = (Quaternion.Inverse(skel.axes[p].postQ) * skel.axes[b].preQ).eulerAngles * Mathf.Deg2Rad;
					pos = (Quaternion.Inverse(skel.bones[p].rotation * skel.axes[p].postQ) * skel.root.rotation)
							* skel.root.InverseTransformVector(skel.bones[b].position - skel.bones[p].position);
				}
				colors[(i)*tex.width + j*2 + 0] = new Color(pos.x, pos.y, pos.z, boneData[b, 0]);
				colors[(i)*tex.width + j*2 + 1] = new Color(rot.x, rot.y, rot.z, boneData[b, 1]);
			}
		tex.SetPixels(colors);
		tex.Apply(false, false);

		// enforce nearest sampler
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.filterMode = FilterMode.Point;
		tex.anisoLevel = 0;
	}
	public static void CreatePlayerMesh(Mesh mesh, Skeleton skel, MotionLayout layout, (Mesh,Transform[])[] sources, float motionRadius=2) {
		const int quality=2, shapeQuality=4;

		var vertices = new List<Vector3>();
		var uvShape  = new List<List<Vector4>>();
		var uvSkin   = Array.ConvertAll(new int[quality], i=>Array.ConvertAll(new int[3], j=>new List<Vector4>()));
		var bindPre  = skel.bones.Select((b,i) => !b ? Matrix4x4.identity :
			Matrix4x4.TRS(skel.root.InverseTransformPoint(b.position),
				Quaternion.Inverse(skel.root.rotation) * b.rotation * skel.axes[i].postQ, new Vector3(1,1,1)).inverse
					* (skel.root.worldToLocalMatrix * b.localToWorldMatrix)).ToArray();

		foreach(var (srcMesh, srcBones) in sources) {
			var srcVertices = srcMesh.vertices;
			var srcNormals  = srcMesh.normals;
			var srcTangents = srcMesh.tangents;
			var srcUVs      = srcMesh.uv;
			MeshUtil.FixNormalTangents(srcMesh, ref srcNormals, ref srcTangents);

			var boneBinds = MeshUtil.RetargetWeightBindposes(
				srcMesh.boneWeights, srcMesh.bindposes, srcBones, skel.bones, (int)HumanBodyBones.Hips, quality:quality);
			for(int v=0; v<srcMesh.vertexCount; v++)
				for(int i=0; i<quality; i++) {
					var (bone, bind) = boneBinds[v, i];
					var weight = bind[3,3];
					bind = bindPre[bone] * bind;
					// store vertex data in bone space
					var vertex  = bind.MultiplyPoint3x4(srcVertices[v]);
					var normal  = bind.MultiplyVector  (srcNormals[v]);
					var tangent = bind.MultiplyVector  (srcTangents[v]);
					uvSkin[i][0].Add(new Vector4(vertex.x,  vertex.y,  vertex.z,  weight/2 + bone));
					uvSkin[i][1].Add(new Vector4(normal.x,  normal.y,  normal.z,  srcUVs[v][i]));
					uvSkin[i][2].Add(new Vector4(tangent.x, tangent.y, tangent.z, srcTangents[v].w));
				}

			var objectToRoot = skel.root.worldToLocalMatrix * skel.bones[(int)HumanBodyBones.Hips].localToWorldMatrix
							* srcMesh.bindposes[Array.IndexOf(srcBones, skel.bones[(int)HumanBodyBones.Hips])];
			vertices.AddRange(srcMesh.vertices.Select(objectToRoot.MultiplyPoint3x4));

			var srcDeltas = new Vector3[srcMesh.vertexCount];
			var dstDeltas = new Vector3[srcMesh.vertexCount];
			var offset = uvShape.Count;
			uvShape.AddRange(Enumerable.Repeat((List<Vector4>)null, srcMesh.vertexCount));
			foreach(var group in layout.shapeIndices.GroupBy(si => si.index)) {
				Array.Clear(dstDeltas, 0, dstDeltas.Length);
				foreach(var si in group) {
					var shape = srcMesh.GetBlendShapeIndex(si.shape);
					if(shape >= 0) {
						srcMesh.GetBlendShapeFrameVertices(shape,
							srcMesh.GetBlendShapeFrameCount(shape)-1, srcDeltas, null, null);
						for(int v=0; v<srcMesh.vertexCount; v++)
							dstDeltas[v] += srcDeltas[v] * si.weight;
					}
				}
				for(int v=0; v<srcMesh.vertexCount; v++)
					if(dstDeltas[v] != Vector3.zero) {
						var normal = srcNormals[v];
						var tangent = (Vector3)srcTangents[v];
						var bitangent = Vector3.Cross(normal.normalized, tangent);
						var m = Matrix4x4.identity;
						m.SetColumn(0, normal);
						m.SetColumn(1, tangent);
						m.SetColumn(2, bitangent);
						// store delta in vertex (normal/tangent/bitangent) space
						var delta = m.inverse.MultiplyVector(dstDeltas[v]);
						uvShape[offset + v] = uvShape[offset + v] ?? new List<Vector4>();
						uvShape[offset + v].Add(new Vector4(delta.x, delta.y, delta.z, group.Key));
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
			var layout = new MotionLayout(skeleton, MotionLayout.defaultHumanLayout);
			layout.AddDecoderVisemeShapes(smrs[0].sharedMesh);

			var tex = (Texture2D)player.sharedMaterial.GetTexture("_Armature");
			var mesh = player.GetComponent<MeshFilter>().sharedMesh;
			CreatePlayerTex(tex, skeleton, layout);
			CreatePlayerMesh(mesh, skeleton, layout,
				smrs.Select(smr => (smr.sharedMesh, smr.bones)).ToArray(), motionRadius:4);
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
	[MenuItem("CONTEXT/SkinnedMeshRenderer/CreateMeshPlayer")]
	static void CreatePlayer(MenuCommand command) {
		var smr = (SkinnedMeshRenderer)command.context;
		var smrs = Selection.gameObjects.Select(x => x.GetComponent<SkinnedMeshRenderer>())
											.Where(x => x).ToArray();
		if(!(smrs.Length > 0 && smrs[0] == smr))
			return;

		var animator = smr.gameObject.GetComponentInParent<Animator>();
		var player = MeshPlayer.CreatePlayer(animator.name + ".Player", animator.transform.parent, animator, smrs,
						MeshPlayer.CreatePlayerPath(animator));
		Selection.activeGameObject = player.gameObject;
	}
}
}
#endif