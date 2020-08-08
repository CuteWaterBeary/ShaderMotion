#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using Path = System.IO.Path;
using UnityEngine;
using UnityEditor;

namespace ShaderMotion {
public class RecorderGen {
	public static void GenRecorderMesh(HumanUtil.Armature arm, FrameLayout layout, Mesh mesh) {
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
			foreach(var j in layout.channels[i]) {
				vertices.Add(Vector3.zero);
				normals. Add(arm.scale * preM.GetColumn(1));
				tangents.Add(arm.scale * preM.GetColumn(2) + new Vector4(0,0,0, slot));
				boneWeights.Add(par >= 0 ? new BoneWeight{boneIndex0=par, weight0=1/arm.scale} : new BoneWeight{});

				vertices.Add(Vector3.zero);
				normals. Add(arm.scale * postM.GetColumn(1));
				tangents.Add(arm.scale * postM.GetColumn(2) + new Vector4(0,0,0, arm.axes[i].sign * (j+1)));
				boneWeights.Add(new BoneWeight{boneIndex0=i, weight0=1/arm.scale});

				vertices.Add(Vector3.zero);
				normals. Add(Vector3.zero);
				tangents.Add(Vector4.zero);
				boneWeights.Add(new BoneWeight{boneIndex0=i, weight0=1/arm.scale});
				slot++;
			}
		}

		mesh.Clear();
		mesh.ClearBlendShapes();
		mesh.subMeshCount = 1;
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
		mesh.SetVertices(vertices);
		mesh.SetNormals (normals );
		mesh.SetTangents(tangents);
		mesh.boneWeights = boneWeights.ToArray();
		mesh.bindposes = bindposes;
		mesh.triangles = Enumerable.Range(0, vertices.Count).ToArray();
		mesh.bounds = bounds;
	}
	[MenuItem("ShaderMotion/Generate Recorder")]
	static void GenRecorderMesh() {
		var animator = Selection.activeGameObject.GetComponentInParent<Animator>();
		if(!(animator && animator.isHuman)) {
			Debug.LogError($"Expect a human Animator on {Selection.activeGameObject}");
			return;
		}
		var path0 = Path.Combine(Path.GetDirectoryName(AssetDatabase.GetAssetPath(animator.avatar)),
							animator.name);

		var smr = animator.transform.Find("MotionRecord")?.GetComponent<SkinnedMeshRenderer>();
		if(!smr) {
			var mesh = new Mesh();
			var path = $"{path0}_record.asset";
			AssetDatabase.CreateAsset(mesh, path);
			Debug.Log($"Create mesh @ {path}");

			var go = new GameObject("MotionRecord", typeof(SkinnedMeshRenderer));
			go.transform.SetParent(animator.transform, false);
			smr = go.GetComponent<SkinnedMeshRenderer>();
			smr.sharedMesh = mesh;
		}
		var mat = smr.sharedMaterial;
		if(!mat)
			smr.sharedMaterial = mat = Resources.Load<Material>("MotionRecorder");

		{
			var mesh = smr.sharedMesh;
			var armature = new HumanUtil.Armature(animator, FrameLayout.defaultHumanBones);
			var layout = new FrameLayout(armature, FrameLayout.defaultOverrides);
			GenRecorderMesh(armature, layout, mesh);
			smr.bones = armature.bones;
			AssetDatabase.SaveAssets();

			if(!smr.rootBone)
				smr.rootBone = animator.transform;
			// rescale the anchor, useful for boundary safety system
			if(smr.rootBone != animator.transform)
				smr.rootBone.localScale = new Vector3(1,1,1) * HumanUtil.GetHumanScale(animator);

			var scale = smr.rootBone.localScale;
			scale = new Vector3(1/scale.x, 1/scale.y, 1/scale.z);
			smr.localBounds = new Bounds(Vector3.Scale(scale,mesh.bounds.center),
										Vector3.Scale(scale,mesh.bounds.size));
		}
	}
}
}
#endif