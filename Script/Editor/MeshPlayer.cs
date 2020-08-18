#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;

namespace ShaderMotion {
public class MeshPlayer {
	public static void CreatePlayerTex(Texture2D tex, HumanUtil.Armature arm, FrameLayout layout) {
		var boneData = new int[arm.bones.Length, 2];
		for(int i=0; i<arm.bones.Length; i++) {
			var start = layout.baseIndices[i] - (layout.channels[i][0] - (layout.channels[i][0] < 3 ? 0 : 3));
			var mask = layout.channels[i].Sum(j => 1<<j);
			boneData[i, 0] = start;
			boneData[i, 1] = (mask & 7) + (arm.axes[i].sign > 0 ? 0 : 8);
		}

		tex.Resize(arm.bones.Length*2, arm.bones.Length, TextureFormat.RGBAFloat, false);
		var colors = Enumerable.Repeat(new Color(0,0,0,-1), tex.width * tex.height).ToArray();
		for(int i=0; i<arm.bones.Length; i++) if(arm.bones[i]) {
			var ancestors = new List<int>();
			for(int b = i; b >= 0; b = arm.parents[b])
				ancestors.Add(b);
			ancestors.Reverse();
			for(int j=0; j<ancestors.Count; j++) {
				var b = ancestors[j];
				var p = arm.parents[b];
				var bone = arm.bones[b];
				var par = p < 0 ? arm.root : arm.bones[p];
				var invQ = Quaternion.Inverse(p < 0 ? par.rotation : par.rotation * arm.axes[p].postQ);
				var pos = (invQ * arm.root.rotation) * arm.root.InverseTransformVector(bone.position - par.position);
				var rot = (invQ * par.rotation * arm.axes[b].preQ).eulerAngles * Mathf.Deg2Rad;
				if(p < 0) // save scale instead of position for motionT
					pos = new Vector3(1,1,1) * arm.scale;
				colors[(i)*tex.width + j*2 + 0] = new Color(pos.x, pos.y, pos.z, boneData[b, 0]);
				colors[(i)*tex.width + j*2 + 1] = new Color(rot.x, rot.y, rot.z, boneData[b, 1]);
			}
		}
		tex.SetPixels(colors);
		tex.Apply(false, false);
	}
	public static void CreatePlayerMesh(Mesh mesh, HumanUtil.Armature arm, FrameLayout layout, Mesh srcMesh, Transform[] srcBones, int quality=2, int shapeQuality=4, float motionRadius=2) {
		var hipsIndex = Array.IndexOf(arm.humanBones, HumanBodyBones.Hips);

		// rescale bone in bindpose because motion armature has no scale
		var bindposes = arm.bones.Select(b => Matrix4x4.Scale(
							(arm.root.worldToLocalMatrix * (b??arm.root).localToWorldMatrix).lossyScale)).ToArray();
		var bwBinds = MeshUtil.RetargetWeightBindposes(srcMesh.boneWeights, srcMesh.bindposes, srcBones, arm.bones, hipsIndex, quality:quality);
		for(int v=0; v<srcMesh.vertexCount; v++)
			for(int i=0; i<quality; i++) {
				var bone = bwBinds[v, i].Item1;
				var bind = bwBinds[v, i].Item2;
				if(bind[3,3] != 0)
					bwBinds[v, i].Item2 = Matrix4x4.Rotate(Quaternion.Inverse(arm.axes[bone].postQ)) * bindposes[bone] * bind;
			}

		// skinning
		var srcVertices = srcMesh.vertices;
		var srcNormals  = srcMesh.normals;
		var srcTangents = srcMesh.tangents;
		MeshUtil.FixNormalTangents(srcMesh, ref srcNormals, ref srcTangents);
		var srcUVs      = srcMesh.uv;
		var uvSkin = Array.ConvertAll(new int[quality],
						x => new[]{new List<Vector4>(), new List<Vector4>(), new List<Vector4>()});
		for(int v=0; v<srcMesh.vertexCount; v++)
			for(int i=0; i<quality; i++) {
				var bone = bwBinds[v, i].Item1;
				var bind = bwBinds[v, i].Item2;
				var weight  = bind[3,3];
				var vertex  = bind.MultiplyPoint3x4(srcVertices[v]);
				var normal  = bind.MultiplyVector  (srcNormals[v]);
				var tangent = bind.MultiplyVector  (srcTangents[v]);

				uvSkin[i][0].Add(new Vector4(vertex.x,  vertex.y,  vertex.z, weight/2 + bone));
				uvSkin[i][1].Add(new Vector4(normal.x,  normal.y,  normal.z, srcUVs[v][i]));
				uvSkin[i][2].Add(new Vector4(tangent.x, tangent.y, tangent.z, srcTangents[v].w));
			}

		// shape
		var uvShape = new List<Vector4>[srcMesh.vertexCount];
		var dverts = new Vector3[srcMesh.vertexCount];
		var dsums  = new Vector3[srcMesh.vertexCount];
		foreach(var sis in layout.shapeIndices.GroupBy(si => si.index)) {
			var slot = -1;
			Array.Clear(dsums, 0, dsums.Length);
			foreach(var si in sis) {
				var shape = srcMesh.GetBlendShapeIndex(si.shape);
				if(shape >= 0) {
					slot = si.index;
					var frame = srcMesh.GetBlendShapeFrameCount(shape)-1;
					srcMesh.GetBlendShapeFrameVertices(shape, frame, dverts, null, null);
					for(int v=0; v<srcMesh.vertexCount; v++)
						dsums[v] += dverts[v] * si.weight;
				}
			}
			for(int v=0; v<srcMesh.vertexCount; v++)
				if(dsums[v] != Vector3.zero) {
					var normal = srcNormals[v];
					var tangent = (Vector3)srcTangents[v];
					var bitangent = Vector3.Cross(normal.normalized, tangent);
					var m = Matrix4x4.identity;
					m.SetColumn(0, normal);
					m.SetColumn(1, tangent);
					m.SetColumn(2, bitangent);
					var dlocal = m.inverse.MultiplyVector(dsums[v]);
					uvShape[v] = uvShape[v] ?? new List<Vector4>();
					uvShape[v].Add((Vector4)dlocal + new Vector4(0,0,0,slot));
				}
		}
		for(int v=0; v<srcMesh.vertexCount; v++)
			if(uvShape[v] != null) {
				// uvShape[v].Sort((d0, d1) => -((Vector3)d0).sqrMagnitude.CompareTo(((Vector3)d1).sqrMagnitude));
				if(uvShape[v].Count > shapeQuality)
					Debug.LogWarning($"vertex has more than {shapeQuality} shapes: {uvShape[v].Count}");
			}

		mesh.ClearBlendShapes();
		mesh.Clear();
		mesh.indexFormat = srcMesh.indexFormat;
		mesh.subMeshCount = srcMesh.subMeshCount;

		var objectToRoot = arm.root.worldToLocalMatrix * arm.bones[hipsIndex].localToWorldMatrix
							* srcMesh.bindposes[Array.IndexOf(srcBones, arm.bones[hipsIndex])];
		mesh.vertices = Array.ConvertAll(srcMesh.vertices, v => objectToRoot.MultiplyPoint3x4(v)); // rest pose
		for(int i=0; i<quality; i++)
			for(int j=0; j<2; j++)
				mesh.SetUVs(i*2+j, uvSkin[i][j]);
		for(int i=0; i<shapeQuality; i++)
			mesh.SetUVs(4+i, uvShape.Select(x => x != null && i<x.Count ? x[i] : Vector4.zero).ToList());
		mesh.SetNormals(uvSkin[0][2].ConvertAll(x => (Vector3)x));
		mesh.SetTangents(uvSkin[1][2]);
		for(int i=0; i<srcMesh.subMeshCount; i++)
			mesh.SetIndices(srcMesh.GetIndices(i, false), srcMesh.GetTopology(i), i, false, (int)srcMesh.GetBaseVertex(i));

		var srcBounds = srcMesh.bounds;
		var center = objectToRoot.MultiplyPoint3x4(srcBounds.center);
		var size = Vector3.Max(objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.left)),
								objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.right)))
				+ Vector3.Max(objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.down)),
								objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.up)))
				+ Vector3.Max(objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.back)),
								objectToRoot.MultiplyVector(Vector3.Scale(srcBounds.size, Vector3.forward)));
		var sizeXZ = Mathf.Max(size.x, size.z) + 2*motionRadius*arm.scale;
		mesh.bounds = new Bounds(center, new Vector3(sizeXZ, size.y, sizeXZ));
	}
	public static MeshRenderer CreatePlayer(string name, Transform parent, Animator animator, SkinnedMeshRenderer smr, string assetPath) {
		var player = (parent ? parent.Find(name) : GameObject.Find("/"+name)?.transform)
							?.GetComponent<MeshRenderer>();
		if(!player) {
			if(!System.IO.Directory.Exists(Path.GetDirectoryName(assetPath)))
				System.IO.Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

			var mesh = new Mesh();
			var tex = new Texture2D(1,1);
			AssetDatabase.CreateAsset(mesh, assetPath + "_player.asset");
			AssetDatabase.CreateAsset(tex,  assetPath + "_armature.asset");

			var mats = new Material[smr.sharedMaterials.Length];
			for(int i=0; i<mats.Length; i++) {
				mats[i] = Object.Instantiate(Resources.Load<Material>("MeshPlayer"));
				mats[i].mainTexture = smr.sharedMaterials[i].mainTexture;
				mats[i].SetTexture("_Armature", tex);
				AssetDatabase.CreateAsset(mats[i],  assetPath + (i == 0 ? "_player.mat" : $"_player{i}.mat"));
			}

			var go = new GameObject(name, typeof(MeshRenderer), typeof(MeshFilter));
			player = go.GetComponent<MeshRenderer>();
			player.transform.SetParent(parent, false);
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
			var arm = new HumanUtil.Armature(animator, FrameLayout.defaultHumanBones);
			var layout = new FrameLayout(arm, FrameLayout.defaultOverrides);
			layout.AddDecoderVisemeShapes(srcMesh);

			CreatePlayerTex(tex, arm, layout);
			CreatePlayerMesh(dstMesh, arm, layout, srcMesh, smr.bones);
			AssetDatabase.SaveAssets();
		}
		return player;
	}
}
class MeshPlayerEditor {
	[MenuItem("CONTEXT/SkinnedMeshRenderer/CreateMeshPlayer")]
	static void CreatePlayer(MenuCommand command) {
		var smr = (SkinnedMeshRenderer)command.context;
		var animator = smr.gameObject.GetComponentInParent<Animator>();
		var assetPath = MeshRecorder.GenerateAssetPath(animator);

		var player = MeshPlayer.CreatePlayer($"{animator.name}.Player", animator.transform.parent,
											animator, smr, assetPath);
		Selection.activeGameObject = player.gameObject;
	}
}
}
#endif