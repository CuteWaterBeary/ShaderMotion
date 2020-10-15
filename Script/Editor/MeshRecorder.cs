#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;

namespace ShaderMotion {
public class MeshRecorder {
	public static void CreateRecorderMesh(Mesh mesh, Skeleton skel, MotionLayout layout) {
		var bindposes = skel.bones.Select(b => Matrix4x4.Scale( // unscale bone in bindpose
							(skel.root.worldToLocalMatrix * (b??skel.root).localToWorldMatrix).lossyScale).inverse).ToArray();
		var bounds = new Bounds();
		var vertices = new List<Vector3>(Enumerable.Repeat(Vector3.zero, 3)); // background quad
		var normals  = new List<Vector3>(Enumerable.Repeat(Vector3.zero, 3));
		var tangents = new List<Vector4>(Enumerable.Repeat(new Vector4(0,0,0, -1), 3));
		var boneWeights = new List<BoneWeight>(Enumerable.Repeat(new BoneWeight(), 3));
		for(int i=0; i<skel.bones.Length; i++) {
			if(skel.bones[i])
				bounds.Encapsulate(skel.root.InverseTransformPoint(skel.bones[i].position));
			var par   = skel.parents[i];
			var preM  = Matrix4x4.Rotate(skel.axes[i].preQ);
			var postM = Matrix4x4.Rotate(skel.axes[i].postQ);
			if(!skel.bones[i]) {
				par = i;
				preM = postM = Matrix4x4.identity;
			}
			var slot = layout.baseIndices[i];
			foreach(var chan in layout.channels[i]) {
				vertices.Add(Vector3.zero);
				normals. Add(skel.scale * preM.GetColumn(1));
				tangents.Add(skel.scale * preM.GetColumn(2) + new Vector4(0,0,0, slot));
				boneWeights.Add(par >= 0 ? new BoneWeight{boneIndex0=par, weight0=1} : new BoneWeight{});

				vertices.Add(Vector3.zero);
				normals. Add(skel.scale * postM.GetColumn(1));
				tangents.Add(skel.scale * postM.GetColumn(2) + new Vector4(0,0,0, chan));
				boneWeights.Add(new BoneWeight{boneIndex0=i, weight0=1});

				vertices.Add(Vector3.zero);
				normals. Add(Vector3.zero);
				tangents.Add(new Vector4(0,0,0, par >= 0 ? skel.axes[i].sign : 0));
				boneWeights.Add(new BoneWeight{boneIndex0=i, weight0=1});
				slot++;
			}
		}
		var slotToVertex = new Dictionary<int, int>();
		foreach(var slot in layout.shapeIndices.Select(si => si.index).Distinct()) {
			var chan = 7;
			var scale = skel.scale/1024;
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
				// move unused vertex a bit because Unity doesn't create empty blendshape
				if(si.weight == 0)
					dvertices[v+1] += new Vector3(0,1e-5f,0);
			}
			mesh.AddBlendShapeFrame(shape, 100, dvertices, null, null);
		}
	}
	public static SkinnedMeshRenderer CreateRecorder(string name, Transform parent, Animator animator, string path) {
		var recorder = (parent ? parent.Find(name) : GameObject.Find("/"+name)?.transform)
							?.GetComponent<SkinnedMeshRenderer>();
		if(!recorder) {
			if(!System.IO.Directory.Exists(Path.GetDirectoryName(path)))
				System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));

			var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
			if(!mesh) {
				mesh = new Mesh();
				AssetDatabase.CreateAsset(mesh, path);
			}

			var go = new GameObject(name, typeof(SkinnedMeshRenderer));
			go.transform.SetParent(parent, false);
			recorder = go.GetComponent<SkinnedMeshRenderer>();
			recorder.rootBone = recorder.transform;
			recorder.sharedMesh = mesh;
			recorder.sharedMaterial = Resources.Load<Material>("MeshRecorder");
		}
		{
			var skeleton = new Skeleton(animator);
			var layout = new MotionLayout(skeleton, MotionLayout.defaultHumanLayout);
			layout.AddEncoderVisemeShapes();
			CreateRecorderMesh(recorder.sharedMesh, skeleton, layout);
			recorder.bones = skeleton.bones;
			AssetDatabase.SaveAssets();

			var bounds = recorder.sharedMesh.bounds;
			recorder.localBounds = new Bounds(bounds.center/skeleton.scale, bounds.size/skeleton.scale);
			recorder.transform.localScale = new Vector3(1,1,1)*skeleton.scale;
		}
		return recorder;
	}
	public static string CreateRecorderPath(Animator animator) {
		var name = animator.avatar.name;
		var path = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)), "auto",
			(name.EndsWith("Avatar") ? name.Substring(0, name.Length-6) : name) + "Recorder.mesh");
		return path.StartsWith("Assets") ? path : Path.Combine("Assets", path);
	}
}
class MeshRecorderEditor {
	[MenuItem("CONTEXT/Animator/CreateMeshRecorder")]
	static void CreateRecorder(MenuCommand command) {
		var animator = (Animator)command.context;
		var recorder = MeshRecorder.CreateRecorder("Recorder", animator.transform, animator,
						MeshRecorder.CreateRecorderPath(animator));
		Selection.activeGameObject = recorder.gameObject;
	}
}
}
#endif