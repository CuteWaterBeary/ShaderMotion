using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using AsyncGPUReadbackRequest = UnityEngine.Rendering.AsyncGPUReadbackRequest;

namespace ShaderMotion {
public class MotionDecoder {
	public readonly Skeleton skeleton;
	public readonly MotionLayout layout;
	public readonly Dictionary<string, float> shapes;
	public readonly (Vector3 t, Quaternion q, float s)[] motions;
	public MotionDecoder(Skeleton skeleton, MotionLayout layout,
						int width=80, int height=45, int tileRadix=3, int tileLen=2) {
		this.skeleton = skeleton;
		this.layout = layout;
		this.shapes = new Dictionary<string, float>();
		this.motions = new (Vector3,Quaternion,float)[skeleton.bones.Length];

		tileCount = new Vector2Int(width/tileLen, height);
		tilePow = (int)System.Math.Pow(tileRadix, tileLen*3);
	}

	const float PositionScale = 2;
	private NativeArray<float> tex = new NativeArray<float>();
	private Vector3Int texSize;
	private Vector2Int tileCount;
	private int tilePow;
	private int layer;
	private float SampleTile(int idx) {
		int x = idx / tileCount.y;
		int y = idx % tileCount.y;
		x += layer/2 * 3;
		if((layer & 1) != 0)
			x = tileCount.x-1-x;

		x *= texSize.x/tileCount.x;
		y *= texSize.y/tileCount.y;
		return tex[((texSize.y-1-y) * texSize.x + x) * texSize.z];
	}
	public void Update(AsyncGPUReadbackRequest req, int layer=0) {
		this.tex = req.GetData<float>();
		this.texSize = new Vector3Int(req.width, req.height, req.layerDataSize/(req.width*req.height*4));
		this.layer = layer;

		var vec = new Vector3[5];
		for(int i=0; i<skeleton.bones.Length; i++) {
			System.Array.Clear(vec, 0, vec.Length);
			var idx = layout.baseIndices[i];
			foreach(var j in layout.channels[i])
				vec[j/3][j%3] = SampleTile(idx++);

			if(layout.channels[i][0] < 3) {
				var swingTwist = vec[0] * 180;
				motions[i] = (swingTwist, BoneAxes.SwingTwist(skeleton.axes[i].sign * swingTwist), float.NaN);
			} else {
				for(int j=0; j<3; j++)
					vec[2][j] = ShaderImpl.DecodeVideoFloat(vec[1][j], vec[2][j], tilePow);
				var (rotY, rotZ) = ShaderImpl.orthogonalize(vec[3], vec[4]);
				if(!(rotZ.magnitude > 0))
					(rotY, rotZ) = (Vector3.up, Vector3.forward);
				motions[i] = (vec[2] * PositionScale,
					Quaternion.LookRotation(rotZ, rotY), rotY.magnitude / rotZ.magnitude);
			}
		}

		shapes.Clear();
		foreach(var si in layout.shapeIndices) {
			float w;
			shapes.TryGetValue(si.shape, out w);
			shapes[si.shape] = w + SampleTile(si.index) * si.weight;
		}
	}
}
}