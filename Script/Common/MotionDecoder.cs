using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using AsyncGPUReadbackRequest = UnityEngine.Rendering.AsyncGPUReadbackRequest;

namespace ShaderMotion {
public class MotionDecoder {
	public readonly Skeleton skeleton;
	public readonly Morph morph;
	public readonly MotionLayout layout;
	public readonly Dictionary<string, float> shapes;
	public readonly (Vector3 t, Quaternion q, float s)[] motions;
	private readonly Vector3Int tileCount;
	public MotionDecoder(Skeleton skeleton, Morph morph, MotionLayout layout, int width=80, int height=45,
						int tileWidth=2, int tileHeight=1, int tileDepth=3, int tileRadix=3) {
		this.skeleton = skeleton;
		this.morph = morph;
		this.layout = layout;
		this.shapes = new Dictionary<string, float>();
		this.motions = new (Vector3,Quaternion,float)[skeleton.bones.Length];

		tileCount = new Vector3Int(width/tileWidth, height/tileHeight,
			(int)System.Math.Pow(tileRadix, tileWidth*tileHeight*tileDepth));
	}

	const float PositionScale = 2;
	const int layerSize = 3;
	private NativeArray<float> tex = new NativeArray<float>();
	private Vector3Int texSize;
	private int layer;
	private float SampleTile(int idx) {
		int x = idx / tileCount.y;
		int y = idx % tileCount.y;
		x += layer/2 * layerSize;
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
		for(int b=0; b<skeleton.bones.Length; b++) {
			System.Array.Clear(vec, 0, vec.Length);
			for(int axis=0; axis<layout.bones[b].Length; axis++)
				if(layout.bones[b][axis] >= 0)
					vec[axis/3][axis%3] = SampleTile(layout.bones[b][axis]);

			if(layout.bones[b].Length <= 3) {
				var swingTwist = vec[0] * 180;
				motions[b] = (swingTwist, HumanAxes.SwingTwist(Vector3.Scale(skeleton.axes[b].sign, swingTwist)), float.NaN);
			} else {
				for(int j=0; j<3; j++)
					vec[2][j] = ShaderImpl.DecodeVideoFloat(vec[1][j], vec[2][j], tileCount.z);
				var (rotY, rotZ) = ShaderImpl.orthogonalize(vec[3], vec[4]);
				if(!(rotZ.magnitude > 0))
					(rotY, rotZ) = (Vector3.up, Vector3.forward);
				motions[b] = (vec[2] * PositionScale,
					Quaternion.LookRotation(rotZ, rotY), rotY.magnitude / rotZ.magnitude);
			}
		}

		shapes.Clear();
		for(int b=0; b<layout.blends.Length; b++) {
			var slot = layout.blends[b];
			var blend = morph.blends[b];
			if(slot >= 0 && blend != null)
				blend.Sample(new Vector2(SampleTile(slot), SampleTile(slot+1)), shapes, writeDefault:true);
		}
	}
}
}