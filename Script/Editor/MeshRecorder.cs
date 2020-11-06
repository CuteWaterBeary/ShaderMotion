#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;

namespace ShaderMotion {
public class MeshRecorder {
	public static Transform[] CreateRecorderMesh(Mesh mesh, Skeleton sk, MotionLayout layout, (Mesh,Transform[]) source) {
		var (srcMesh, srcBones) = source;

		var boneIdx   = new int[sk.bones.Length];
		var bones     = new List<Transform>(srcBones ?? new Transform[0]);
		var bindposes = new List<Matrix4x4>(srcMesh?.bindposes ?? new Matrix4x4[0]);
		for(int i=0; i<sk.bones.Length; i++) {
			boneIdx[i] = bones.IndexOf(sk.bones[i]);
			if(boneIdx[i] < 0 && sk.bones[i]) {
				boneIdx[i] = bones.Count;
				bones.Add(sk.bones[i]);
				bindposes.Add(Matrix4x4.identity);
			}
		}
		var vertices = new List<Vector3>();
		var normals  = new List<Vector3>();
		var tangents = new List<Vector4>();
		var uvs      = new List<Vector2>();
		var boneWeights = new List<BoneWeight>();
		srcMesh?.GetVertices(vertices);
		srcMesh?.GetNormals (normals);
		srcMesh?.GetTangents(tangents);
		srcMesh?.GetUVs     (0, uvs);
		srcMesh?.GetBoneWeights(boneWeights);

		var baseVertex = vertices.Count;
		{ // background bone
			vertices.AddRange(Enumerable.Repeat(Vector3.zero, 2)); 
			normals .AddRange(Enumerable.Repeat(Vector3.zero, 2));
			tangents.AddRange(Enumerable.Repeat(Vector4.zero, 2));
			uvs     .AddRange(Enumerable.Repeat(new Vector2(-1, 0), 2));
			boneWeights.AddRange(Enumerable.Repeat(new BoneWeight(), 2));
		}
		for(int i=0; i<sk.bones.Length; i++) if(sk.bones[i] && !sk.dummy[i]) {
			var p = sk.parents[i];
			while(p >= 0 && sk.dummy[p])
				p = sk.parents[p];

			var postM = (Matrix4x4.Scale((sk.root.worldToLocalMatrix
				* sk.bones[i].localToWorldMatrix).lossyScale) * bindposes[boneIdx[i]]).inverse
				* Matrix4x4.Rotate(sk.axes[i].postQ);

			var preM  = p < 0 ? Matrix4x4.identity : (Matrix4x4.Scale((sk.root.worldToLocalMatrix
				* sk.bones[p].localToWorldMatrix).lossyScale) * bindposes[boneIdx[p]]).inverse
				* Matrix4x4.Rotate(Quaternion.Inverse(sk.bones[p].rotation) * sk.bones[i].parent.rotation
				* sk.axes[i].preQ);

			var slot = layout.baseIndices[i];
			foreach(var chan in layout.channels[i]) {
				vertices.Add(preM.GetColumn(3));
				normals. Add(sk.humanScale * preM.GetColumn(1));
				tangents.Add(sk.humanScale * preM.GetColumn(2));
				uvs     .Add(new Vector2(slot, 0));
				boneWeights.Add(new BoneWeight{boneIndex0=boneIdx[p >= 0 ? p : i], weight0=1});

				vertices.Add(postM.GetColumn(3));
				normals. Add(sk.humanScale * postM.GetColumn(1));
				tangents.Add(sk.humanScale * postM.GetColumn(2));
				uvs     .Add(new Vector2(chan, p >= 0 ? sk.axes[i].sign : 0));
				boneWeights.Add(new BoneWeight{boneIndex0=boneIdx[i], weight0=1});

				slot++;
			}
		}
		var slotToVertex = new Dictionary<int, int>();
		foreach(var slot in layout.shapeIndices.Select(si => si.index).Distinct()) {
			int chan = 7, sign = 1;
			var scale = sk.humanScale/1024;
			var postM = Matrix4x4.identity;
			slotToVertex[slot] = vertices.Count + 1;

			vertices.Add(Vector3.zero);
			normals. Add(scale * postM.GetColumn(1));
			tangents.Add(scale * postM.GetColumn(2) + new Vector4(0,0,0, slot));
			uvs     .Add(new Vector2(slot, 0));
			boneWeights.Add(new BoneWeight{weight0=1});

			vertices.Add(Vector3.zero);
			normals. Add(scale * postM.GetColumn(1));
			tangents.Add(scale * postM.GetColumn(2) + new Vector4(0,0,0, chan));
			uvs     .Add(new Vector2(chan, sign));
			boneWeights.Add(new BoneWeight{weight0=1});
		}
		// experimental: make dynamic bounds encapsulate view position
		var viewpos = sk.root.Find("viewpos");
		if(viewpos) {
			var i = (int)HumanBodyBones.Neck;
			for(int j=0; j<2; j++) {
				vertices[baseVertex+j] = bindposes[boneIdx[i]].inverse.MultiplyPoint3x4(
								sk.bones[i].InverseTransformPoint(viewpos.TransformPoint(j * Vector3.forward)));
				boneWeights[baseVertex+j] = new BoneWeight{boneIndex0=boneIdx[i], weight0=1};
			}
		}

		mesh.Clear();
		mesh.SetVertices(vertices);
		mesh.SetNormals (normals );
		mesh.SetTangents(tangents);
		mesh.SetUVs     (0, uvs);
		mesh.boneWeights = boneWeights.ToArray();
		mesh.bindposes   = bindposes.ToArray();

		mesh.indexFormat  = srcMesh?.indexFormat ?? UnityEngine.Rendering.IndexFormat.UInt16;
		mesh.subMeshCount = (srcMesh?.subMeshCount ?? 0) + 1;
		for(int i=0; i<mesh.subMeshCount-1; i++)
			mesh.SetIndices(srcMesh.GetIndices(i, false), srcMesh.GetTopology(i),
				i, false, (int)srcMesh.GetBaseVertex(i));
		
		mesh.SetIndices(Enumerable.Range(0, vertices.Count-baseVertex).ToArray(), MeshTopology.Lines,
			mesh.subMeshCount-1, false, baseVertex);

		// bounds encapsulates mesh and bones and it's symmetric along Y-axis
		var bounds = new Bounds();
		foreach(var bone in sk.bones) if(bone)
			bounds.Encapsulate(sk.root.InverseTransformPoint(bone.position));
		if(srcMesh)
			for(int i=0;i<2;i++)
			for(int j=0;j<2;j++)
			for(int k=0;k<2;k++)
				bounds.Encapsulate(sk.root.InverseTransformPoint(srcBones[0].TransformPoint(
					srcMesh.bindposes[0].MultiplyPoint3x4(srcMesh.bounds.min
						+ Vector3.Scale(srcMesh.bounds.size, new Vector3(i,j,k))))));
		mesh.bounds = new Bounds(bounds.center, Vector3.Max(bounds.size, new Vector3(bounds.size.z,0,bounds.size.x)));

		// blendshapes comes from mesh and layout
		var srcDV = new Vector3[srcMesh?.vertexCount??0];
		var dstDV = new Vector3[vertices.Count];
		var shapes = Enumerable.Range(0, srcMesh?.blendShapeCount??0).Select(i => srcMesh.GetBlendShapeName(i))
							.Concat(layout.shapeIndices.Select(si => si.shape)).Distinct();
		foreach(var shape in shapes) {
			System.Array.Clear(dstDV, 0, dstDV.Length);
			var shapeIndex = srcMesh?.GetBlendShapeIndex(shape) ?? -1;
			if(shapeIndex >= 0) {
				srcMesh.GetBlendShapeFrameVertices(shapeIndex, // TODO: only last frame is handled
					srcMesh.GetBlendShapeFrameCount(shapeIndex)-1, srcDV, null, null);
				System.Array.Copy(srcDV, 0, dstDV, 0, srcDV.Length);
			}
			foreach(var si in layout.shapeIndices)
				if(si.shape == shape) {
					var v = slotToVertex[si.index];
					dstDV[v] += normals[v]*2 * si.weight;
				}
			mesh.AddBlendShapeFrame(shape, 100, dstDV, null, null);
		}
		return bones.ToArray();
	}
	public static SkinnedMeshRenderer CreateRecorder(string name, Transform parent, Animator animator, SkinnedMeshRenderer smr, string path) {
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

			var go = smr ? Object.Instantiate(smr.gameObject) : new GameObject("", typeof(SkinnedMeshRenderer));
			go.name = name;
			go.SetActive(true);
			go.transform.SetParent(parent, false);
			go.transform.localPosition = Vector3.zero;
			go.transform.localRotation = Quaternion.identity;

			recorder = go.GetComponent<SkinnedMeshRenderer>();
			recorder.rootBone = recorder.transform;
			recorder.sharedMesh = mesh;
		}
		{
			var skeleton = new Skeleton(animator);
			var layout = new MotionLayout(skeleton, MotionLayout.defaultHumanLayout);
			layout.AddEncoderVisemeShapes(smr?.sharedMesh);
			var bones = CreateRecorderMesh(recorder.sharedMesh, skeleton, layout, (smr?.sharedMesh, smr?.bones));
			var materials = (smr?.sharedMaterials ?? new Material[0]) .Append(Resources.Load<Material>("MeshRecorder")).ToArray();
			recorder.bones = bones;
			if(recorder.sharedMaterials.Length != materials.Length)
				recorder.sharedMaterials = materials;

			AssetDatabase.SaveAssets();

			recorder.localBounds = recorder.sharedMesh.bounds;
			recorder.transform.localScale = new Vector3(1,1,1);
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
		var recorder = MeshRecorder.CreateRecorder("Recorder", animator.transform, animator, null,
						MeshRecorder.CreateRecorderPath(animator));
		EditorGUIUtility.PingObject(recorder);
	}
	[MenuItem("CONTEXT/SkinnedMeshRenderer/CreateMeshRecorder")]
	static void CreateRecorderSMR(MenuCommand command) {
		var smr = (SkinnedMeshRenderer)command.context;
		var animator = smr.gameObject.GetComponentInParent<Animator>();
		var recorder = MeshRecorder.CreateRecorder("Recorder", animator.transform, animator, smr,
						MeshRecorder.CreateRecorderPath(animator));
		EditorGUIUtility.PingObject(recorder);
	}
}
}
#endif