#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;

namespace ShaderMotion {
public class PlayerGen {
	public static void GenPlayerTex(HumanUtil.Armature arm, FrameLayout layout, Texture2D tex) {
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
	public static void GenPlayerMesh(HumanUtil.Armature arm, FrameLayout layout, Mesh mesh, Mesh srcMesh, Transform[] srcBones, int quality=2, int shapeQuality=4) {
		var hipsIndex = Array.IndexOf(arm.humanBones, HumanBodyBones.Hips);

		// rescale bone in bindpose because motion armature has no scale
		var bindposes = arm.bones.Select(b => Matrix4x4.Scale(
							(arm.root.worldToLocalMatrix * (b??arm.root).localToWorldMatrix).lossyScale)).ToArray();
		var bwBinds = MeshUtil.MergeBoneWeightBindposes(srcMesh.boneWeights, srcMesh.bindposes, srcBones, arm.bones, quality:quality, rootBone:hipsIndex);
		for(int v=0; v<srcMesh.vertexCount; v++)
			for(int i=0; i<quality; i++) {
				var bone = bwBinds[v, i].Key;
				var bind = bwBinds[v, i].Value;
				if(bind[3,3] != 0)
					bwBinds[v, i] = new KeyValuePair<int, Matrix4x4>(bone,
										Matrix4x4.Rotate(Quaternion.Inverse(arm.axes[bone].postQ)) * bindposes[bone] * bind);
			}

		var srcVertices = srcMesh.vertices;
		var srcNormals  = srcMesh.normals;
		var srcTangents = srcMesh.tangents;
		var srcUVs      = srcMesh.uv;
		var uvSkin = Array.ConvertAll(new int[quality],
						x => new[]{new List<Vector4>(), new List<Vector4>(), new List<Vector4>()});
		for(int v=0; v<srcMesh.vertexCount; v++)
			for(int i=0; i<quality; i++) {
				var bone = bwBinds[v, i].Key;
				var bind = bwBinds[v, i].Value;
				var weight  = bind[3,3];
				var vertex  = bind.MultiplyPoint3x4(srcVertices[v]);
				var normal  = bind.MultiplyVector  (srcNormals[v]);
				var tangent = bind.MultiplyVector  (srcTangents[v]);

				uvSkin[i][0].Add(new Vector4(vertex.x,  vertex.y,  vertex.z, weight/2 + bone));
				uvSkin[i][1].Add(new Vector4(normal.x,  normal.y,  normal.z, srcUVs[v][i]));
				uvSkin[i][2].Add(new Vector4(tangent.x, tangent.y, tangent.z, 0));
			}

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
					var bitangent = Vector3.Cross(normal, tangent);
					var m = Matrix4x4.identity;
					m.SetColumn(0, normal);
					m.SetColumn(1, tangent);
					m.SetColumn(2, bitangent);
					var dlocal = m.inverse.MultiplyPoint3x4(dsums[v]);
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

		mesh.Clear();
		mesh.ClearBlendShapes();
		mesh.subMeshCount = 1;
		mesh.indexFormat = srcMesh.vertexCount < 65536 ? UnityEngine.Rendering.IndexFormat.UInt16
													: UnityEngine.Rendering.IndexFormat.UInt32;


		var objectToRoot = arm.root.worldToLocalMatrix * arm.bones[hipsIndex].localToWorldMatrix
							* srcMesh.bindposes[Array.IndexOf(srcBones, arm.bones[hipsIndex])];
		mesh.vertices = Array.ConvertAll(srcMesh.vertices, v => objectToRoot.MultiplyPoint3x4(v)); // rest pose
		mesh.triangles = srcMesh.triangles;
		for(int i=0; i<quality; i++)
			for(int j=0; j<2; j++)
				mesh.SetUVs(i*2+j, uvSkin[i][j]);
		for(int i=0; i<shapeQuality; i++)
			mesh.SetUVs(4+i, uvShape.Select(x => x != null && i<x.Count ? x[i] : Vector4.zero).ToList());
		mesh.SetNormals(uvSkin[0][2].ConvertAll(x => (Vector3)x));
		mesh.SetTangents(uvSkin[1][2]);

		var bounds = mesh.bounds;
		bounds.min -= new Vector3(1,0,1) * arm.scale;
		bounds.max += new Vector3(1,0,1) * arm.scale;
		mesh.bounds = bounds;
	}
	[MenuItem("ShaderMotion/Generate Player")]
	static void GenPlayerMesh() {
		var smr = Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>();
		if(!smr) {
			Debug.LogError($"Require a SkinnedMeshRenderer on {Selection.activeGameObject}");
			return;
		}
		var animator = smr.gameObject.GetComponentInParent<Animator>();
		if(!(animator && animator.isHuman)) {
			Debug.LogError($"Expect a human Animator");
			return;
		}
		var path0 = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)),
							animator.name);

		var mr = smr.transform.parent.Find("MotionPlayer")?.GetComponent<MeshRenderer>();
		if(!mr) {
			var mesh = new Mesh();
			var path = $"{path0}_player.asset";
			AssetDatabase.CreateAsset(mesh, path);
			Debug.Log($"Create mesh @ {path}");

			var go = new GameObject("MotionPlayer", typeof(MeshRenderer), typeof(MeshFilter));
			go.transform.SetParent(animator.transform, false);
			mr = go.GetComponent<MeshRenderer>();
			mr.GetComponent<MeshFilter>().sharedMesh = mesh;
		}
		var mat = mr.sharedMaterial;
		if(!mat) {
			mr.sharedMaterial = mat = Object.Instantiate(Resources.Load<Material>("MotionPlayer"));
			var path = $"{path0}_player.mat";
			AssetDatabase.CreateAsset(mat, path);
			Debug.Log($"Create material @ {path}");

			mat.mainTexture = smr.sharedMaterial.mainTexture;
			mat.color = smr.sharedMaterial.color;
		}
		var tex = (Texture2D)mat.GetTexture("_Armature");
		if(!tex) {
			tex = new Texture2D(1,1);
			var path = $"{path0}_armature.asset";
			AssetDatabase.CreateAsset(tex, path);
			Debug.Log($"Create texture @ {path}");

			mat.SetTexture("_Armature", tex);
		}

		var srcMesh = smr.sharedMesh;
		var dstMesh = mr.GetComponent<MeshFilter>().sharedMesh;
		var arm = new HumanUtil.Armature(animator, FrameLayout.defaultHumanBones);
		var layout = new FrameLayout(arm, FrameLayout.defaultOverrides);
		layout.AddDecoderVisemeShapes(srcMesh);

		GenPlayerTex(arm, layout, tex);
		GenPlayerMesh(arm, layout, dstMesh, srcMesh, smr.bones);

		AssetDatabase.SaveAssets();
	}
}
}
#endif