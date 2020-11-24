#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;

namespace ShaderMotion {
public class MeshRecorder {
	public static Transform[] CreateRecorderMesh(Mesh mesh, Skeleton skel, Appearance appr, MotionLayout layout, (Mesh,Transform[]) source, bool lineMesh=false) {
		var (srcMesh, srcBones) = source;

		var boneIdx   = new int[skel.bones.Length];
		var bones     = new List<Transform>(srcBones ?? new Transform[0]);
		var bindposes = new List<Matrix4x4>(srcMesh?.bindposes ?? new Matrix4x4[0]);
		for(int b=0; b<skel.bones.Length; b++) {
			boneIdx[b] = bones.IndexOf(skel.bones[b]);
			if(boneIdx[b] < 0 && skel.bones[b]) {
				boneIdx[b] = bones.Count;
				bones.Add(skel.bones[b]);
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
		for(int b=0; b<skel.bones.Length; b++) if(skel.bones[b] && !skel.dummy[b]) {
			var p = skel.parents[b];
			while(p >= 0 && skel.dummy[p])
				p = skel.parents[p];

			var mat1 = (Matrix4x4.Scale((skel.root.worldToLocalMatrix
				* skel.bones[b].localToWorldMatrix).lossyScale/skel.humanScale) * bindposes[boneIdx[b]]).inverse
				* Matrix4x4.Rotate(skel.axes[b].postQ);

			var mat0 = p < 0 ? mat1 : (Matrix4x4.Scale((skel.root.worldToLocalMatrix
				* skel.bones[p].localToWorldMatrix).lossyScale/skel.humanScale) * bindposes[boneIdx[p]]).inverse
				* Matrix4x4.Rotate(Quaternion.Inverse(skel.bones[p].rotation) * skel.bones[b].parent.rotation
									* skel.axes[b].preQ);

			foreach(var (axis, index) in layout.bones[b].Select((x,y) => (y,x))) if(index >= 0) {
				vertices.Add(mat0.GetColumn(3));
				normals. Add(mat0.GetColumn(1));
				tangents.Add(mat0.GetColumn(2));
				uvs     .Add(new Vector2(index, 0));
				boneWeights.Add(new BoneWeight{boneIndex0=boneIdx[p < 0 ? b : p], weight0=1});

				vertices.Add(mat1.GetColumn(3));
				normals. Add(mat1.GetColumn(1));
				tangents.Add(mat1.GetColumn(2));
				uvs     .Add(new Vector2(axis, p >= 0 ? skel.axes[b].sign : 0));
				boneWeights.Add(new BoneWeight{boneIndex0=boneIdx[b], weight0=1});
			}
		}
		var exprVertex = new int[appr.exprShapes.Length];
		{
			int chan = 7, sign = 1;
			var mat1 = (Matrix4x4.Scale((skel.root.worldToLocalMatrix
				* skel.bones[0].localToWorldMatrix).lossyScale/(skel.humanScale*1e-4f)) * bindposes[boneIdx[0]]).inverse
				* Matrix4x4.Rotate(skel.axes[0].postQ);

			foreach(var (e, index) in layout.exprs.Select((x,y) => (y,x))) if(index >= 0) {
				exprVertex[e] = vertices.Count + 1;

				vertices.Add(mat1.GetColumn(3));
				normals. Add(mat1.GetColumn(1));
				tangents.Add(mat1.GetColumn(2));
				uvs     .Add(new Vector2(index, 0));
				boneWeights.Add(new BoneWeight{boneIndex0=boneIdx[0], weight0=1});

				vertices.Add(mat1.GetColumn(3));
				normals. Add(mat1.GetColumn(1));
				tangents.Add(mat1.GetColumn(2));
				uvs     .Add(new Vector2(chan, sign));
				boneWeights.Add(new BoneWeight{boneIndex0=boneIdx[0], weight0=1});
			}
		}
		
		// experimental: make dynamic bounds encapsulate view position
		var viewpos = skel.root.Find("viewpos");
		if(viewpos) {
			var i = (int)HumanBodyBones.Neck;
			for(int j=0; j<2; j++) {
				vertices[baseVertex+j] = bindposes[boneIdx[i]].inverse.MultiplyPoint3x4(
								skel.bones[i].InverseTransformPoint(viewpos.TransformPoint(j * Vector3.forward)));
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
		if(lineMesh)
			mesh.SetIndices(Enumerable.Range(0, vertices.Count-baseVertex).ToArray(),
				MeshTopology.Lines, mesh.subMeshCount-1, false, baseVertex);
		else
			mesh.SetIndices(Enumerable.Range(0, (vertices.Count-baseVertex)/2*3).Select(i => i/3*2+i%3%2).ToArray(),
				MeshTopology.Triangles, mesh.subMeshCount-1, false, baseVertex);

		// bounds encapsulates mesh and bones and it's symmetric along Y-axis
		var bounds = new Bounds();
		foreach(var bone in skel.bones) if(bone)
			bounds.Encapsulate(skel.root.InverseTransformPoint(bone.position));
		if(srcMesh)
			for(int i=0;i<2;i++)
			for(int j=0;j<2;j++)
			for(int k=0;k<2;k++)
				bounds.Encapsulate(skel.root.InverseTransformPoint(srcBones[0].TransformPoint(
					srcMesh.bindposes[0].MultiplyPoint3x4(srcMesh.bounds.min
						+ Vector3.Scale(srcMesh.bounds.size, new Vector3(i,j,k))))));
		mesh.bounds = new Bounds(bounds.center, Vector3.Max(bounds.size, new Vector3(bounds.size.z,0,bounds.size.x)));

		// blendshapes comes from mesh and layout
		const float PositionScale = 2;
		var srcDV = new Vector3[srcMesh?.vertexCount??0];
		var dstDV = new Vector3[vertices.Count];
		var shapes = Enumerable.Range(0, srcMesh?.blendShapeCount??0).Select(i => srcMesh.GetBlendShapeName(i))
							.Concat(appr.exprShapes.SelectMany(l => l.Select(s => s.Key))).Distinct();
		foreach(var shape in shapes) {
			System.Array.Clear(dstDV, 0, dstDV.Length);
			var shapeIndex = srcMesh?.GetBlendShapeIndex(shape) ?? -1;
			if(shapeIndex >= 0) {
				srcMesh.GetBlendShapeFrameVertices(shapeIndex, // TODO: only last frame is handled
					srcMesh.GetBlendShapeFrameCount(shapeIndex)-1, srcDV, null, null);
				System.Array.Copy(srcDV, 0, dstDV, 0, srcDV.Length);
			}
			for(int i=0; i<appr.exprShapes.Length; i++)
				foreach(var s in appr.exprShapes[i])
					if(s.Key == shape) {
						var v = exprVertex[i];
						dstDV[v] += normals[v]*PositionScale * s.Value;
					}
			mesh.AddBlendShapeFrame(shape, 100, dstDV, null, null);
		}
		return bones.ToArray();
	}
	public static SkinnedMeshRenderer CreateRecorder(string name, Transform parent, Animator animator, SkinnedMeshRenderer smr, string path, bool lineMesh=false) {
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
			recorder.sharedMaterials = new Material[]{Resources.Load<Material>("MeshRecorder")};
		}
		{
			var skeleton = new Skeleton(animator);
			var appr = new Appearance(smr?.sharedMesh, false);
			var layout = new MotionLayout(skeleton, MotionLayout.defaultHumanLayout,
											appr, MotionLayout.defaultExprLayout);

			recorder.bones = CreateRecorderMesh(recorder.sharedMesh, skeleton, appr, layout,
												(smr?.sharedMesh, smr?.bones), lineMesh:lineMesh);
			recorder.sharedMaterials = (smr?.sharedMaterials ?? new Material[0]).Append(
				recorder.sharedMaterials.LastOrDefault()).ToArray();
			recorder.localBounds = recorder.sharedMesh.bounds;
			recorder.transform.localScale = Vector3.one;
			EditorUtility.SetDirty(recorder);
			AssetDatabase.SaveAssets();
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
	static void CreateRecorder(Animator animator, SkinnedMeshRenderer smr, bool lineMesh) {
		EditorGUIUtility.PingObject(MeshRecorder.CreateRecorder("Recorder", animator.transform,
			animator, smr, MeshRecorder.CreateRecorderPath(animator), lineMesh:lineMesh));
	}
	[MenuItem("CONTEXT/Animator/CreateMeshRecorder")]
	static void CreateRecorderFromAnimator(MenuCommand command) {
		CreateRecorder((Animator)command.context, null, false);
	}
	[MenuItem("CONTEXT/Animator/CreateMeshRecorder(Outline)")]
	static void CreateRecorderFromAnimatorWithLine(MenuCommand command) {
		CreateRecorder((Animator)command.context, null, true);
	}
	[MenuItem("CONTEXT/SkinnedMeshRenderer/CreateMeshRecorder")]
	static void CreateRecorderFromSkinnedMeshRenderer(MenuCommand command) {
		var smr = (SkinnedMeshRenderer)command.context;
		CreateRecorder(smr.gameObject.GetComponentInParent<Animator>(), smr, false);
	}
}
}
#endif