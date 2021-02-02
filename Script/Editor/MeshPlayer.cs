#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;

namespace ShaderMotion {
class MeshPlayerGen {
	public Skeleton skel;
	public Morph morph;
	public MotionLayout layout;
	Vector2[] CreateShapeTex(List<Color> colors, Mesh mesh, Matrix4x4[] vertMatrices, int width=256, float eps=1e-6f) {
		var ranges = new Vector2[mesh.vertexCount];
		var blends = new List<Vector4>[mesh.vertexCount];
		var dstDV = Enumerable.Range(0, 9).Select(_ => new Vector3[mesh.vertexCount]).ToArray();
		foreach(var (slot, blend) in layout.blends.Select((x,y) => (x, morph.blends[y])))
			if(slot >= 0 && blend != null) {
				for(int i=0; i<3; i++)
				for(int j=0; j<3; j++)
					if(i!=1 || j!=1) {
						System.Array.Clear(dstDV[i+j*3], 0, dstDV[i+j*3].Length);
						Morph.GetBlendShapeVertices(mesh, blend.shapes[i,j], dstDV[i+j*3], dstDV[4]);
					}
				System.Array.Clear(dstDV[4], 0, dstDV[4].Length); // origin has no morph
				Debug.Assert(blend.shapes[1,1] == null);

				var vec = new Vector4[9];
				for(int v=0; v<mesh.vertexCount; v++) {
					var empty = true;
					for(int k=0; k<9; k++) {
						empty = empty && dstDV[k][v].magnitude < eps;
						vec[k] = vertMatrices[v].MultiplyVector(dstDV[k][v]);
						vec[k].w = slot;
					}
					if(empty)
						continue;

					if(blends[v] == null)
						blends[v] = new List<Vector4>();
					blends[v].AddRange(vec);
				}
			}
		for(int v=0; v<mesh.vertexCount; v++)
			if(blends[v] != null) {
				// allocate in a single row
				var cnt = blends[v].Count/9;
				var rem = (colors.Count+width-1)%width+1;
				colors.AddRange(Enumerable.Repeat(default(Color), (rem+cnt*3 > width ? width*3-rem : 0) + cnt*3));

				var pos = colors.Count-cnt*3-width*2;
				for(int k=0; k<cnt; k++)
				for(int i=0; i<3; i++)
				for(int j=0; j<3; j++)
					colors[pos+k*3+i+j*width] = (Color)blends[v][k*9+i+j*3];
				ranges[v] = new Vector2(cnt, pos+1+width); // length goes first so its default is 0
			}
		colors.AddRange(Enumerable.Repeat(default(Color), (width - colors.Count%width)%width));
		return ranges;
	}
	void CreateBoneTex(List<Color> colors, Matrix4x4[] bindposes) {
		var axesData = new Vector4[skel.bones.Length];
		var restPose = new Matrix4x4[skel.bones.Length];
		for(int b=0; b<skel.bones.Length; b++) if(skel.bones[b]) {
			var slot0 = layout.bones[b].Select((slot, axis) => slot<0 ? null : (int?)(slot-axis)).Max()??0;
			var sign = skel.axes[b].sign;
			for(int i=0; i<3; i++)
				if(layout.bones[b][i] < 0)
					sign[i] = 0;
			var p = skel.parents[b];
			if(p < 0)
				axesData[b] = (Vector4)(Vector3.one / skel.humanScale) + new Vector4(0,0,0, -1-(slot0+3));
			else
				axesData[b] = (Vector4)sign + new Vector4(0,0,0, slot0);
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
			var vertMatrices = System.Array.ConvertAll(MeshUtil.RetargetBindWeights(
				srcBones, skel.bones, srcMesh.bindposes.Select(x => x * objectToRoot.inverse).ToArray(), bindposes,
				boneWeights), m => m * objectToRoot);
			var shapeUV = CreateShapeTex(colors, srcMesh, vertMatrices);
			vertices.AddRange(srcMesh.vertices.Select((v,i) => vertMatrices[i].MultiplyPoint3x4(v)));
			normals .AddRange(srcMesh.normals .Select((v,i) => vertMatrices[i].MultiplyVector(v)));
			tangents.AddRange(srcMesh.tangents.Select((v,i) =>
							new Vector4(0,0,0, v.w) + (Vector4)vertMatrices[i].MultiplyVector(v)));
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

			var skel = new Skeleton(animator);
			var morph = new Morph(animator);
			var layout = new MotionLayout(skel, morph);
			var sources = smrs.Select(smr => (smr.sharedMesh, smr.bones)).ToArray();
			var gen = new MeshPlayerGen{skel=skel, morph=morph, layout=layout};
			gen.CreatePlayer(mesh, texs["Bone"], texs["Shape"], sources);
			MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Low);

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