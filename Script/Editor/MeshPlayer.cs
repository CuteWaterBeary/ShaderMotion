#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;

namespace ShaderMotion {
struct MeshPlayerGen {
	public Skeleton skel;
	public Appearance appr;
	public MotionLayout layout;
	Vector2[] CreateShapeTex(List<Color> colors, Mesh mesh, Matrix4x4[] transforms, int width=256) {
		var shapes = new List<Color>[mesh.vertexCount];
		var ranges = new Vector2[mesh.vertexCount];
		var srcDV = new Vector3[mesh.vertexCount];
		var dstDV = new Vector3[mesh.vertexCount];
		for(int e=0; e<appr.exprShapes.Length; e++) {
			System.Array.Clear(dstDV, 0, dstDV.Length);
			foreach(var s in appr.exprShapes[e]) {
				var shape = mesh.GetBlendShapeIndex(s.Key);
				if(shape >= 0) {
					mesh.GetBlendShapeFrameVertices(shape, mesh.GetBlendShapeFrameCount(shape)-1, srcDV, null, null);
					for(int v=0; v<mesh.vertexCount; v++)
						dstDV[v] += srcDV[v] * s.Value;
				}
			}
			for(int v=0; v<mesh.vertexCount; v++)
				if(dstDV[v].sqrMagnitude != 0) {
					var dv = transforms[v].MultiplyVector(dstDV[v]);
					shapes[v] = shapes[v] ?? new List<Color>();
					shapes[v].Add(new Color(dv.x, dv.y, dv.z, layout.exprs[e]));
				}
		}
		for(int v=0; v<mesh.vertexCount; v++)
			if(shapes[v] != null) {
				// make sure the shapes of a vertex fit in a row
				if(colors.Count%width + shapes[v].Count > width)
					colors.AddRange(Enumerable.Repeat(new Color(), width - colors.Count%width));
				ranges[v] = new Vector2(colors.Count, shapes[v].Count);
				colors.AddRange(shapes[v]);
			}
		colors.AddRange(Enumerable.Repeat(new Color(), (width - colors.Count%width)%width));
		return ranges;
	}
	void CreateBoneTex(List<Color> colors, Matrix4x4[] bindposes) {
		var axesData = new Vector4[skel.bones.Length];
		var restPose = new Matrix4x4[skel.bones.Length];
		for(int b=0; b<skel.bones.Length; b++) if(skel.bones[b]) {
			var p = skel.parents[b];
			var idx0 = layout.bones[b].Select((idx, axis) => idx < 0 ? 0 : idx - (axis - (axis<3?0:3))).Max();
			var mask = layout.bones[b].Select((idx, axis) => idx < 0 ? 0 : 1<<axis).Sum();
			if(p < 0)
				axesData[b] = new Vector4(1,1,1, 0) / skel.humanScale + new Vector4(0,0,0, -1-idx0);
			else
				axesData[b] = new Vector4((mask>>0)&1, (mask>>1)&1, (mask>>2)&1, 0) * skel.axes[b].sign
							+ new Vector4(0,0,0, idx0);
			if(p < 0)
				restPose[b] = Matrix4x4.identity;
			else if(skel.bones[p] == skel.bones[b])
				restPose[b] = Matrix4x4.Rotate(Quaternion.Inverse(skel.axes[p].preQ) * skel.axes[b].preQ);
			else
				restPose[b] = Matrix4x4.Rotate(Quaternion.Inverse(skel.bones[p].rotation * skel.axes[p].postQ))
							* Matrix4x4.Translate(skel.root.rotation * skel.root.InverseTransformVector(
											skel.bones[b].position - skel.bones[p].position))
							* Matrix4x4.Rotate(skel.bones[b].parent.rotation * skel.axes[b].preQ);
		}
		var mats = Enumerable.Range(0, skel.bones.Length).Select(_ => new List<Matrix4x4>()).ToArray();
		for(int b=0; b<skel.bones.Length; b++) if(skel.bones[b]) {
			var mat = Matrix4x4.Rotate(Quaternion.Inverse(skel.bones[b].rotation * skel.axes[b].postQ) * skel.root.rotation)
						* Matrix4x4.Translate(-skel.root.InverseTransformPoint(skel.bones[b].position))
						* (skel.root.worldToLocalMatrix * skel.bones[b].localToWorldMatrix) * bindposes[b];
			for(var p = b; p >= 0; p = skel.parents[p]) {
				mat.SetRow(3, axesData[p]);
				mats[b].Add(mat);
				mat = restPose[p];
			}
		}
		var width = mats.Max(l=>l.Count);
		colors.Clear();
		colors.AddRange(mats.SelectMany(l=>l.Concat(Enumerable.Repeat(new Matrix4x4(), width-l.Count)))
					.SelectMany(m=>new Color[]{m.GetColumn(0), m.GetColumn(1), m.GetColumn(2), m.GetColumn(3)}));
	}
	public void CreatePlayer(Mesh mesh, Texture2D boneTex, Texture2D shapeTex, (Mesh,Transform[])[] sources) {
		var vertices = new List<Vector3>();
		var normals  = new List<Vector3>();
		var tangents = new List<Vector4>();
		var uvs      = new[]{new List<Vector4>(), new List<Vector4>()};
		var bindposes = new Matrix4x4[skel.bones.Length];
		var colors = new List<Color>();
		foreach(var (srcMesh, srcBones) in sources) {
			var objectToRoot = skel.root.worldToLocalMatrix * srcBones[0].localToWorldMatrix * srcMesh.bindposes[0];
			var boneWeights  = srcMesh.boneWeights;
			var transforms   = System.Array.ConvertAll(MeshUtil.RetargetBindWeights(
				srcBones, skel.bones, srcMesh.bindposes.Select(x => x * objectToRoot.inverse).ToArray(), bindposes,
				boneWeights), m => m * objectToRoot);
			var shapeUV = CreateShapeTex(colors, srcMesh, transforms);
			vertices.AddRange(srcMesh.vertices.Select((v,i) => transforms[i].MultiplyPoint3x4(v)));
			normals .AddRange(srcMesh.normals .Select((v,i) => transforms[i].MultiplyVector(v)));
			tangents.AddRange(srcMesh.tangents.Select((v,i) =>
							new Vector4(0,0,0, v.w) + (Vector4)transforms[i].MultiplyVector(v)));
			normals .AddRange(Enumerable.Repeat(Vector3.zero, vertices.Count-normals .Count)); // pad missing vectors
			tangents.AddRange(Enumerable.Repeat(Vector4.zero, vertices.Count-tangents.Count));
			uvs[0].AddRange(srcMesh.uv.Select((uv, v) => new Vector4(uv.x, uv.y, shapeUV[v].x, shapeUV[v].y)));
			uvs[1].AddRange(boneWeights.Select(bw => new Vector4(
				bw.boneIndex0+Mathf.Clamp01(bw.weight0)/2, bw.boneIndex1+Mathf.Clamp01(bw.weight1)/2,
				bw.boneIndex2+Mathf.Clamp01(bw.weight2)/2, bw.boneIndex3+Mathf.Clamp01(bw.weight3)/2)));
		}
		shapeTex.Resize(256, colors.Count/256, TextureFormat.RGBAFloat, false);
		shapeTex.SetPixels(colors.ToArray());
		shapeTex.Apply(false, false);
		CreateBoneTex(colors, bindposes);
		boneTex.Resize(colors.Count/skel.bones.Length, skel.bones.Length, TextureFormat.RGBAFloat, false);
		boneTex.SetPixels(colors.ToArray());
		boneTex.Apply(false, false);

		mesh.Clear();
		mesh.SetVertices(vertices);
		mesh.SetNormals (normals);
		mesh.SetTangents(tangents);
		for(int i=0; i<8; i++)
			mesh.SetUVs(i, i<uvs.Length ? uvs[i] : null);
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
	}
}
public class MeshPlayer {
	public static MeshRenderer CreatePlayer(string name, Transform parent, Animator animator, SkinnedMeshRenderer[] smrs, string path) {
		var player = (parent ? parent.Find(name) : GameObject.Find("/"+name)?.transform)?.GetComponent<MeshRenderer>();
		if(!player) {
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));

			var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
			if(!mesh) {
				mesh = new Mesh();
				AssetDatabase.CreateAsset(mesh, path);
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
			var mesh = player.GetComponent<MeshFilter>().sharedMesh;
			var texs = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(mesh))
							.Where(a=>a is Texture2D).ToDictionary(a=>a.name, a=>(Texture2D)a);
			var texNames = new HashSet<string>{"Bone", "Shape"};
			foreach(var texName in texNames) {
				if(!texs.ContainsKey(texName)) {
					var tex = new Texture2D(1,1);
					tex.name = texName;
					AssetDatabase.AddObjectToAsset(tex, mesh);
					texs[texName] = tex;
					foreach(var mat in player.sharedMaterials) {
						mat.shader = Resources.Load<Material>("MeshPlayer").shader;
						mat.SetTexture($"_{texName}", tex);
					}
				}
			}
			foreach(var tex in texs.Values.Where(t => !texNames.Contains(t.name)).ToArray())
				Object.DestroyImmediate(tex, true);

			var skeleton = new Skeleton(animator);
			var appr = new Appearance(smrs[0].sharedMesh, true);
			var layout = new MotionLayout(skeleton, MotionLayout.defaultHumanLayout,
											appr, MotionLayout.defaultExprLayout);
			var sources = smrs.Select(smr => (smr.sharedMesh, smr.bones)).ToArray();
			var gen = new MeshPlayerGen{skel=skeleton, appr=appr, layout=layout};
			gen.CreatePlayer(mesh, texs["Bone"], texs["Shape"], sources);

			MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Low);
			mesh.UploadMeshData(false);
			// make bounds rotational invariant and extend by motion radius
			const float motionRadius = 4;
			var size = mesh.bounds.size;
			var sizeXZ = Mathf.Max(size.x, size.z) + 2*motionRadius;
			mesh.bounds = new Bounds(mesh.bounds.center, new Vector3(sizeXZ, size.y, sizeXZ));

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