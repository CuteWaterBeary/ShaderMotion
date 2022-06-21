using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShaderMotion {
public class MeshRecorderGen {
	public Skeleton skel;
	public Morph morph;
	public MotionLayout layout;
	int getParentAndMatrixPair(int b, out Matrix4x4 mat0, out Matrix4x4 mat1) {
		var p = skel.parents[b];
		while(p >= 0 && skel.dummy[p])
			p = skel.parents[p];
		mat1 = (Matrix4x4.Scale((skel.root.worldToLocalMatrix
			* skel.bones[b].localToWorldMatrix).lossyScale/skel.humanScale)).inverse
			* Matrix4x4.Rotate(skel.axes[b].postQ);
		mat0 = p < 0 ? Matrix4x4.identity : (Matrix4x4.Scale((skel.root.worldToLocalMatrix
			* skel.bones[p].localToWorldMatrix).lossyScale/skel.humanScale)).inverse
			* Matrix4x4.Rotate(Quaternion.Inverse(skel.bones[p].rotation) * skel.bones[b].parent.rotation
								* skel.axes[b].preQ);
		return p;
	}
	public void CreateTexture(Texture2D tex) {
		var mats = new List<Matrix4x4>();
		// background quad
		mats.Add(new Matrix4x4(Vector4.zero, Vector4.zero, Vector4.zero, new Vector4(-1, 3, 1, 0)).transpose);
		mats.Add(default(Matrix4x4));
		for(int b=0; b<skel.bones.Length; b++) if(skel.bones[b] && !skel.dummy[b] && layout.bones[b] != null) {
			Matrix4x4 mat0, mat1;
			var p = getParentAndMatrixPair(b, out mat0, out mat1);
			var range = layout.bones[b].Select((slot, axis) => (slot:slot, axis:axis)).Where(x => x.slot >= 0);
			var first = range.FirstOrDefault();
			mat0.SetRow(3, new Vector4(first.slot, first.axis, range.Count(), p+1));
			mat1.SetRow(3, skel.axes[b].sign);
			mats.Add(mat0);
			mats.Add(mat1);
		} else
			mats.AddRange(new Matrix4x4[2]);

		tex.Resize(8, mats.Count/2, TextureFormat.RGBAFloat, false);
		tex.SetPixels(mats.SelectMany(m => 
			new Color[]{m.GetColumn(0), m.GetColumn(1), m.GetColumn(2), m.GetColumn(3)}).ToArray());
		tex.Apply(false, false);
	}
	Dictionary<int, int> CreateVertices(Mesh mesh, int[] boneMap) {
		var bindposes = mesh.bindposes;
		var uvs = new List<Vector2>(mesh.uv);
		var boneWeights = new List<BoneWeight>(mesh.boneWeights);
		var matrices   = new List<Matrix4x4>();
		var slotToVert = new Dictionary<int, int>();
		{ // background quad
			uvs.AddRange(new[]{new Vector2(-1/*slot*/, 0), new Vector2(3/*axis*/, 0/*sign*/)});
			matrices.AddRange(new Matrix4x4[2]);
			boneWeights.AddRange(new BoneWeight[2]);
		}
		for(int b=0; b<skel.bones.Length; b++) if(skel.bones[b] && !skel.dummy[b] && layout.bones[b] != null) {
			Matrix4x4 mat0, mat1;
			var p = getParentAndMatrixPair(b, out mat0, out mat1);
			mat1 = bindposes[boneMap[b]].inverse * mat1;
			mat0 = bindposes[boneMap[p < 0 ? b : p]].inverse * mat0;
			foreach(var (axis, slot) in layout.bones[b].Select((x,y) => (y,x))) if(slot >= 0) {
				slotToVert[slot] = boneWeights.Count + 1;
				uvs.Add(new Vector2(slot, 0));
				matrices.Add(mat0); boneWeights.Add(new BoneWeight{boneIndex0=boneMap[p < 0 ? b : p], weight0=1});
				uvs.Add(new Vector2(axis, axis < 3 ? skel.axes[b].sign[axis] : 0));
				matrices.Add(mat1); boneWeights.Add(new BoneWeight{boneIndex0=boneMap[b], weight0=1});
			}
		}
		{ // blendshape quads
			var mat1 = Matrix4x4.TRS(bindposes[boneMap[0]].inverse.GetColumn(3), Quaternion.identity,
				2e-5f * Vector3.one); // unity discards blendshape with |delta| < 1e-5
			foreach(var slot in layout.blends.Where(x => x >= 0).SelectMany(x => new[]{x, x+1})) {
				slotToVert[slot] = boneWeights.Count + 1;
				uvs.Add(new Vector2(slot, 0));
				matrices.Add(mat1); boneWeights.Add(new BoneWeight{boneIndex0=boneMap[0], weight0=1});
				uvs.Add(new Vector2(7/*axis*/, 1/*sign*/));
				matrices.Add(mat1); boneWeights.Add(new BoneWeight{boneIndex0=boneMap[0], weight0=1});
			}
		}
		
		var vertices = new List<Vector3>(mesh.vertices);
		var normals  = new List<Vector3>(mesh.normals);
		var tangents = new List<Vector4>(mesh.tangents);
		foreach(var mat in matrices) {
			vertices.Add(mat.GetColumn(3));
			normals. Add(mat.GetColumn(1));
			tangents.Add(mat.GetColumn(2));
		}
		mesh.SetVertices(vertices);
		mesh.SetNormals (normals );
		mesh.SetTangents(tangents);
		mesh.SetUVs     (0, uvs);
		mesh.boneWeights = boneWeights.ToArray();
		return slotToVert;
	}
	public Transform[] CreateMeshSkinned(Mesh mesh, Mesh srcMesh, Transform[] srcBones, bool line=false) {
		var boneMap   = new int[skel.bones.Length];
		var bones     = new List<Transform>(srcBones ?? new Transform[0]);
		var bindposes = new List<Matrix4x4>(srcMesh?.bindposes ?? new Matrix4x4[0]);
		for(int b=0; b<skel.bones.Length; b++) {
			boneMap[b] = bones.IndexOf(skel.bones[b]);
			if(boneMap[b] < 0 && skel.bones[b]) {
				boneMap[b] = bones.Count;
				bones.Add(skel.bones[b]);
				bindposes.Add(Matrix4x4.identity);
			}
		}

		mesh.Clear();
		mesh.bindposes = bindposes.ToArray();
		mesh.vertices = srcMesh?.vertices;
		mesh.normals  = srcMesh?.normals;
		mesh.tangents = srcMesh?.tangents;
		mesh.uv       = srcMesh?.uv;
		mesh.boneWeights = srcMesh?.boneWeights;
		
		var baseVertex = mesh.vertexCount;
		var slotToVert = CreateVertices(mesh, boneMap);

		mesh.indexFormat  = srcMesh?.indexFormat ?? UnityEngine.Rendering.IndexFormat.UInt16;
		mesh.subMeshCount = (srcMesh?.subMeshCount ?? 0) + 1;
		for(int i=0; i<mesh.subMeshCount-1; i++)
			mesh.SetIndices(srcMesh.GetIndices(i, false), srcMesh.GetTopology(i),
				i, false, (int)srcMesh.GetBaseVertex(i));
		if(line)
			mesh.SetIndices(Enumerable.Range(0, mesh.vertexCount-baseVertex).ToArray(),
				MeshTopology.Lines, mesh.subMeshCount-1, false, baseVertex);
		else
			mesh.SetIndices(Enumerable.Range(0, (mesh.vertexCount-baseVertex)/2*3).Select(i => i/3*2+i%3%2).ToArray(),
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
		var normals = mesh.normals;
		var srcDV = new Vector3[srcMesh?.vertexCount??0];
		var dstDV = new Vector3[mesh.vertexCount];
		var shapeNames = Enumerable.Range(0, srcMesh?.blendShapeCount??0).Select(i => srcMesh.GetBlendShapeName(i))
							.Concat(morph.controls.Keys).Distinct();
		foreach(var name in shapeNames) {
			System.Array.Clear(dstDV, 0, dstDV.Length);
			var shapes = new Dictionary<string, float>();
			if(morph.controls.ContainsKey(name)) {
				var (b, coord) = morph.controls[name];
				var slot = layout.blends[b];
				var blend = morph.blends[b];
				for(int axis=0; axis<2; axis++)
					dstDV[slotToVert[slot+axis]] += normals[slotToVert[slot+axis]]*(coord[axis]*PositionScale);
				if(blend != null) // use blended shapes
					blend.Sample(coord, shapes);
			} else // otherwise use existing shapes
				shapes[name] = 1f; 

			if(srcMesh)
				Morph.GetBlendShapeVertices(srcMesh, shapes, dstDV, srcDV);
			mesh.AddBlendShapeFrame(name, 100, dstDV, null, null);
		}
		return bones.ToArray();
	}
	public static void CreateMeshInstanced(Mesh mesh) {
		const int axisCount = 12;
		var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
		mesh.Clear();
		mesh.vertices = new Vector3[axisCount*quad.vertexCount];
		mesh.SetUVs(0, Enumerable.Range(0, axisCount).SelectMany(i =>
			quad.uv.Select(v => new Vector3(v.x, v.y, i))).ToList());
		mesh.triangles = Enumerable.Range(0, axisCount).SelectMany(i =>
			quad.triangles.Select(v => v+i*quad.vertexCount)).ToArray();
	}
}
}