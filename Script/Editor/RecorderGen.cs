#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;

namespace ShaderMotion {
public class RecorderGen {
	public static void CreateRecorderMesh(HumanUtil.Armature arm, FrameLayout layout, Mesh mesh) {
		var bindposes = arm.bones.Select(b => Matrix4x4.Scale( // unscale bone in bindpose
							(arm.root.worldToLocalMatrix * (b??arm.root).localToWorldMatrix).lossyScale).inverse).ToArray();
		var bounds = new Bounds();
		var vertices = new List<Vector3>(Enumerable.Repeat(Vector3.zero, 3)); // background quad
		var normals  = new List<Vector3>(Enumerable.Repeat(Vector3.zero, 3));
		var tangents = new List<Vector4>(Enumerable.Repeat(new Vector4(0,0,0, -1), 3));
		var boneWeights = new List<BoneWeight>(Enumerable.Repeat(new BoneWeight(), 3));
		for(int i=0; i<arm.bones.Length; i++) {
			if(arm.bones[i])
				bounds.Encapsulate(arm.root.InverseTransformPoint(arm.bones[i].position));
			var par   = arm.parents[i];
			var preM  = Matrix4x4.Rotate(arm.axes[i].preQ);
			var postM = Matrix4x4.Rotate(arm.axes[i].postQ);
			if(!arm.bones[i]) {
				par = i;
				preM = postM = Matrix4x4.identity;
			}
			var slot = layout.baseIndices[i];
			foreach(var chan in layout.channels[i]) {
				vertices.Add(Vector3.zero);
				normals. Add(arm.scale * preM.GetColumn(1));
				tangents.Add(arm.scale * preM.GetColumn(2) + new Vector4(0,0,0, slot));
				boneWeights.Add(par >= 0 ? new BoneWeight{boneIndex0=par, weight0=1} : new BoneWeight{});

				vertices.Add(Vector3.zero);
				normals. Add(arm.scale * postM.GetColumn(1));
				tangents.Add(arm.scale * postM.GetColumn(2) + new Vector4(0,0,0, chan));
				boneWeights.Add(new BoneWeight{boneIndex0=i, weight0=1});

				vertices.Add(Vector3.zero);
				normals. Add(Vector3.zero);
				tangents.Add(new Vector4(0,0,0, par >= 0 ? arm.axes[i].sign : 0));
				boneWeights.Add(new BoneWeight{boneIndex0=i, weight0=1});
				slot++;
			}
		}
		var slotToVertex = new Dictionary<int, int>();
		foreach(var slot in layout.shapeIndices.Select(si => si.index).Distinct()) {
			var chan = 7;
			var scale = arm.scale/1024;
			var postM = Matrix4x4.identity;
			slotToVertex[slot] = vertices.Count + 1;

			vertices.Add(Vector3.zero);
			normals. Add(scale * postM.GetColumn(1));
			tangents.Add(scale * postM.GetColumn(2) + new Vector4(0,0,0, slot));
			boneWeights.Add(new BoneWeight{weight0=1});

			vertices.Add(Vector3.zero);
			normals. Add(scale * postM.GetColumn(1));
			tangents.Add(scale * postM.GetColumn(2) + new Vector4(0,0,0, chan));
			boneWeights.Add(new BoneWeight{weight0=1});

			vertices.Add(Vector3.zero);
			normals. Add(Vector3.zero);
			tangents.Add(new Vector4(0,0,0, 1));
			boneWeights.Add(new BoneWeight{weight0=1});
		}

		mesh.ClearBlendShapes();
		mesh.Clear();
		mesh.subMeshCount = 1;
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
		mesh.SetVertices(vertices);
		mesh.SetNormals (normals );
		mesh.SetTangents(tangents);
		mesh.boneWeights = boneWeights.ToArray();
		mesh.bindposes = bindposes;
		mesh.triangles = Enumerable.Range(0, vertices.Count).ToArray();

		var sizeXZ = Mathf.Max(bounds.size.x, bounds.size.z);
		mesh.bounds = new Bounds(bounds.center, new Vector3(sizeXZ, bounds.size.y, sizeXZ));
		
		var dvertices = new Vector3[vertices.Count];
		foreach(var sis in layout.shapeIndices.GroupBy(si => si.shape)) {
			string shape = null;
			Array.Clear(dvertices, 0, dvertices.Length);
			foreach(var si in sis) {
				shape = si.shape;
				var v = slotToVertex[si.index];
				dvertices[v] += normals[v]*2 * si.weight;
			}
			mesh.AddBlendShapeFrame(shape, 100, dvertices, null, null);
		}
	}
	static SkinnedMeshRenderer CreateRecorder(SkinnedMeshRenderer recorder, Animator animator, string assetPrefix) {
		if(!recorder) {
			var mesh = new Mesh();
			AssetDatabase.CreateAsset(mesh, assetPrefix + "_recorder.asset");

			var go = new GameObject("", typeof(SkinnedMeshRenderer));
			recorder = go.GetComponent<SkinnedMeshRenderer>();
			recorder.rootBone = animator.transform;
			recorder.sharedMesh = mesh;
			recorder.sharedMaterial = Resources.Load<Material>("MotionRecorder");
		}

		{
			var armature = new HumanUtil.Armature(animator, FrameLayout.defaultHumanBones);
			var layout = new FrameLayout(armature, FrameLayout.defaultOverrides);
			layout.AddEncoderVisemeShapes();
			CreateRecorderMesh(armature, layout, recorder.sharedMesh);
			recorder.bones = armature.bones;
			AssetDatabase.SaveAssets();

			if(recorder.rootBone == animator.transform)
				recorder.localBounds = recorder.sharedMesh.bounds;
		}
		return recorder;
	}
	[MenuItem("ShaderMotion/Create Shader Recorder")]
	static void CreateRecorder() {
		var animator = Selection.activeGameObject.GetComponentInParent<Animator>();
		if(!(animator && animator.isHuman)) {
			Debug.LogError($"Expect a human Animator on {Selection.activeGameObject}");
			return;
		}

		var parent = animator.transform;
		var name = "Recorder";
		var assetPrefix = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)),
							"auto", animator.name);
		if(!System.IO.Directory.Exists(Path.GetDirectoryName(assetPrefix)))
			System.IO.Directory.CreateDirectory(Path.GetDirectoryName(assetPrefix));

		var recorder0 = parent.Find(name)?.GetComponent<SkinnedMeshRenderer>();
		var recorder = CreateRecorder(recorder0, animator, assetPrefix);
		if(!recorder0) {
			recorder.name = name;
			recorder.transform.SetParent(parent, false);
		}
		Selection.activeGameObject = recorder.gameObject;
	}
}
}
#endif