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
		var boneData = new int[skel.bones.Length, 2];
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
				var bone = skel.bones[b];
				var par = p < 0 ? skel.root : skel.bones[p];
				var invQ = Quaternion.Inverse(p < 0 ? par.rotation : par.rotation * skel.axes[p].postQ);
				var pos = (invQ * skel.root.rotation) * skel.root.InverseTransformVector(bone.position - par.position);
				var rot = (invQ * par.rotation * skel.axes[b].preQ).eulerAngles * Mathf.Deg2Rad;
				if(p < 0) // save scale instead of position for rootT
					pos = new Vector3(1,1,1) * skel.scale;
				colors[(i)*tex.width + j*2 + 0] = new Color(pos.x, pos.y, pos.z, boneData[b, 0]);
				colors[(i)*tex.width + j*2 + 1] = new Color(rot.x, rot.y, rot.z, boneData[b, 1]);
			}
		tex.SetPixels(colors);
		tex.Apply(false, false);
	}
	public static void CreatePlayerMesh(Mesh mesh, Skeleton skel, MotionLayout layout, Mesh srcMesh, Transform[] srcBones, int quality=2, int shapeQuality=4, float motionRadius=2) {
		var hipsIndex = (int)HumanBodyBones.Hips;
		var bindposes = skel.bones.Select(b => Matrix4x4.Scale( // bake bone de-scaling into bindpose
							(skel.root.worldToLocalMatrix * (b??skel.root).localToWorldMatrix).lossyScale)).ToArray();
		var boneWeightBindposes = MeshUtil.RetargetWeightBindposes(srcMesh.boneWeights, srcMesh.bindposes,
																srcBones, skel.bones, hipsIndex, quality:quality);

		// skinning
		var uvSkin = Array.ConvertAll(new int[quality], i=>Array.ConvertAll(new int[3], j=>new List<Vector4>()));
		var srcVertices = srcMesh.vertices;
		var srcNormals  = srcMesh.normals;
		var srcTangents = srcMesh.tangents;
		var srcUVs      = srcMesh.uv;
		MeshUtil.FixNormalTangents(srcMesh, ref srcNormals, ref srcTangents);
		for(int v=0; v<srcMesh.vertexCount; v++)
			for(int i=0; i<quality; i++) {
				var bone   = boneWeightBindposes[v, i].Item1;
				var bind   = boneWeightBindposes[v, i].Item2;
				var weight = bind[3,3];
				bind = Matrix4x4.Rotate(Quaternion.Inverse(skel.axes[bone].postQ)) * bindposes[bone] * bind;
				// store vertex data in bone space
				var vertex  = bind.MultiplyPoint3x4(srcVertices[v]);
				var normal  = bind.MultiplyVector  (srcNormals[v]);
				var tangent = bind.MultiplyVector  (srcTangents[v]);
				uvSkin[i][0].Add(new Vector4(vertex.x,  vertex.y,  vertex.z,  weight/2 + bone));
				uvSkin[i][1].Add(new Vector4(normal.x,  normal.y,  normal.z,  srcUVs[v][i]));
				uvSkin[i][2].Add(new Vector4(tangent.x, tangent.y, tangent.z, srcTangents[v].w));
			}

		// shape
		var uvShape = new List<Vector4>[srcMesh.vertexCount];
		var srcDeltas = new Vector3[srcMesh.vertexCount];
		var dstDeltas = new Vector3[srcMesh.vertexCount];
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
					uvShape[v] = uvShape[v] ?? new List<Vector4>();
					uvShape[v].Add(new Vector4(delta.x, delta.y, delta.z, group.Key));
				}
		}
		for(int v=0; v<srcMesh.vertexCount; v++)
			if(uvShape[v] != null) {
				// uvShape[v].Sort((d0, d1) => -((Vector3)d0).sqrMagnitude.CompareTo(((Vector3)d1).sqrMagnitude));
				if(uvShape[v].Count > shapeQuality)
					Debug.LogWarning($"vertex has more than {shapeQuality} shapes: {uvShape[v].Count}");
			}

		var objectToRoot = skel.root.worldToLocalMatrix * skel.bones[hipsIndex].localToWorldMatrix
							* srcMesh.bindposes[Array.IndexOf(srcBones, skel.bones[hipsIndex])];

		mesh.ClearBlendShapes();
		mesh.Clear();
		mesh.indexFormat = srcMesh.indexFormat;
		mesh.subMeshCount = srcMesh.subMeshCount;
		mesh.vertices = Array.ConvertAll(srcMesh.vertices, objectToRoot.MultiplyPoint3x4); // make outline look better 
		for(int i=0; i<srcMesh.subMeshCount; i++)
			mesh.SetIndices(srcMesh.GetIndices(i, false), srcMesh.GetTopology(i), i, false, (int)srcMesh.GetBaseVertex(i));
		// store uvSkin in uv[0~3], normals, tangents
		mesh.SetNormals(uvSkin[0][2].ConvertAll(x => (Vector3)x));
		mesh.SetTangents(uvSkin[1][2]);
		for(int i=0; i<quality; i++)
			for(int j=0; j<2; j++)
				mesh.SetUVs(i*2+j, uvSkin[i][j]);
		// store uvShape in uv[4~7]
		for(int i=0; i<shapeQuality; i++)
			mesh.SetUVs(4+i, uvShape.Select(x => i < (x?.Count??0) ? x[i] : Vector4.zero).ToList());

		// make bounds rotational invariant and extend by motion radius
		var srcBounds = srcMesh.bounds;
		var center = objectToRoot.MultiplyPoint3x4(srcBounds.center);
		var size = Vector3.Max(objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.left)),
								objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.right)))
				+ Vector3.Max(objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.down)),
								objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.up)))
				+ Vector3.Max(objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.back)),
								objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.forward)));
		var sizeXZ = Mathf.Max(size.x, size.z) + 2*motionRadius*skel.scale;
		mesh.bounds = new Bounds(center, new Vector3(sizeXZ, size.y, sizeXZ));
	}
	public static MeshRenderer CreatePlayer(string name, Transform parent, Animator animator, SkinnedMeshRenderer smr, string assetPath) {
		var player = (parent ? parent.Find(name) : GameObject.Find("/"+name)?.transform)
							?.GetComponent<MeshRenderer>();
		if(!player) {
			var assetPathNoExt = Path.ChangeExtension(assetPath, null);
			if(!System.IO.Directory.Exists(Path.GetDirectoryName(assetPath)))
				System.IO.Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

			var mesh = new Mesh();
			AssetDatabase.CreateAsset(mesh, assetPath);

			var tex = new Texture2D(1,1);
			tex.name = "Armature";
			AssetDatabase.AddObjectToAsset(tex, mesh);

			var mats = new Material[smr.sharedMaterials.Length];
			for(int i=0; i<mats.Length; i++) {
				mats[i] = Object.Instantiate(Resources.Load<Material>("MeshPlayer"));
				mats[i].mainTexture = smr.sharedMaterials[i].mainTexture;
				mats[i].SetTexture("_Armature", tex);
				AssetDatabase.CreateAsset(mats[i],  assetPathNoExt + (i == 0 ? ".mat" : $"{i}.mat"));
			}

			var go = new GameObject(name, typeof(MeshRenderer), typeof(MeshFilter));
			go.transform.SetParent(parent, false);
			player = go.GetComponent<MeshRenderer>();
			player.GetComponent<MeshFilter>().sharedMesh = mesh;
			player.sharedMaterials = mats;
			// copy renderer settings
			player.lightProbeUsage				= smr.lightProbeUsage;
			player.reflectionProbeUsage			= smr.reflectionProbeUsage;
			player.shadowCastingMode			= smr.shadowCastingMode;
			player.receiveShadows 				= smr.receiveShadows;
			player.motionVectorGenerationMode	= smr.motionVectorGenerationMode;
			player.allowOcclusionWhenDynamic	= smr.allowOcclusionWhenDynamic;
		}
		{
			var tex = (Texture2D)player.sharedMaterial.GetTexture("_Armature");
			var srcMesh = smr.sharedMesh;
			var dstMesh = player.GetComponent<MeshFilter>().sharedMesh;
			var skeleton = new Skeleton(animator);
			var layout = new MotionLayout(skeleton, MotionLayout.defaultHumanLayout);
			layout.AddDecoderVisemeShapes(srcMesh);

			CreatePlayerTex(tex, skeleton, layout);
			CreatePlayerMesh(dstMesh, skeleton, layout, srcMesh, smr.bones);
			AssetDatabase.SaveAssets();
		}
		return player;
	}
	public static string CreatePlayerPath(Animator animator, SkinnedMeshRenderer smr) {
		var aname = animator.avatar.name;
		var mname = smr.sharedMesh.name;
		var path = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)), "auto",
			(aname.EndsWith("Avatar") ? aname.Substring(0, aname.Length-6) : aname)
			+ (char.ToUpper(mname[0]) + mname.Substring(1)) + "Player.asset");
		return path.StartsWith("Assets") ? path : Path.Combine("Assets", path);
	}
}
class MeshPlayerEditor {
	[MenuItem("CONTEXT/SkinnedMeshRenderer/CreateMeshPlayer")]
	static void CreatePlayer(MenuCommand command) {
		var smr = (SkinnedMeshRenderer)command.context;
		var animator = smr.gameObject.GetComponentInParent<Animator>();
		var player = MeshPlayer.CreatePlayer(animator.name + ".Player", animator.transform.parent, animator, smr,
						MeshPlayer.CreatePlayerPath(animator, smr));
		Selection.activeGameObject = player.gameObject;
	}
}
}
#endif