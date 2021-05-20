using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ShaderMotion {
public class MeshPlayerGen {
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
	void CreateBoneTex(Texture2D boneTex, Matrix4x4[] bindposes) {
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
				restPose[b] = default; // unused value
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
		var maxCount = mats.Max(l => l.Count);
		boneTex.Resize(maxCount*4, skel.bones.Length, TextureFormat.RGBAFloat, false);
		boneTex.SetPixels(mats.SelectMany(l=>l.Concat(Enumerable.Repeat(new Matrix4x4(), maxCount-l.Count)))
			.SelectMany(m=>new Color[]{m.GetColumn(0), m.GetColumn(1), m.GetColumn(2), m.GetColumn(3)}).ToArray());
		boneTex.Apply(false);
	}
	public void CreatePlayer(Mesh mesh, Texture2D boneTex, Texture2D shapeTex, (Mesh,Transform[])[] sources) {
		var vertices = new List<Vector3>();
		var normals  = new List<Vector3>();
		var tangents = new List<Vector4>();
		var uvs      = new[]{new List<Vector4>(), new List<Vector4>()};
		var bindposes = new Matrix4x4[skel.bones.Length];
		var colors = new List<Color>();
		foreach(var (srcMesh, srcBones) in sources) {
			var srcBindposes = srcMesh.bindposes;
			var boneWeights  = srcMesh.boneWeights;
			if(srcBindposes.Length != srcBones.Length && srcBones.Length == 1) { // non-skinned mesh renderer
				srcBindposes = new[]{Matrix4x4.identity};
				boneWeights = Enumerable.Repeat(new BoneWeight{weight0=1}, srcMesh.vertexCount).ToArray();
			}
			var objectToRoot = skel.root.worldToLocalMatrix * srcBones[0].localToWorldMatrix * srcBindposes[0];
			var vertMatrices = System.Array.ConvertAll(MeshUtil.RetargetBindposesBoneWeights(
				srcBones, skel.bones, srcBindposes.Select(x => x * objectToRoot.inverse).ToArray(), bindposes,
				boneWeights), m => m * objectToRoot); // bake objectToRoot into vertex position
			var shapeUV = CreateShapeTex(colors, srcMesh, vertMatrices);
			var vectorScale = 1f;
		#if UNITY_EDITOR
			if(MeshUtility.GetMeshCompression(mesh) != ModelImporterMeshCompression.Off)
				vectorScale = objectToRoot.lossyScale.x; // undo scaling to reduce precision loss on normals
		#endif
			vertices.AddRange(srcMesh.vertices.Select((v,i) => vertMatrices[i].MultiplyPoint3x4(v)));
			normals .AddRange(srcMesh.normals .Select((v,i) => vertMatrices[i].MultiplyVector(v)/vectorScale));
			tangents.AddRange(srcMesh.tangents.Select((v,i) =>
							new Vector4(0,0,0, v.w) + (Vector4)vertMatrices[i].MultiplyVector(v)/vectorScale));
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
		CreateBoneTex(boneTex, bindposes);

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
}