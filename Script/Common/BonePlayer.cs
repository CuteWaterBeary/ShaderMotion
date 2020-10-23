using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using UnityEngine;
using Unity.Collections;
using AsyncGPUReadbackRequest = UnityEngine.Rendering.AsyncGPUReadbackRequest;

namespace ShaderMotion {
public class BonePlayer {
	Skeleton skeleton;
	MotionLayout layout;
	public SkinnedMeshRenderer shapeRenderer;
	public int layer;

	public Vector3    hipsT;
	public Quaternion hipsQ;
	public float      hipsS;
	public Vector3[]  boneSwingTwists;
	public Dictionary<string, float> shapes;
	public BonePlayer(Skeleton skeleton, MotionLayout layout,
						int width=80, int height=45, int tileRadix=3, int tileLen=2) {
		this.skeleton = skeleton;
		this.layout = layout;

		boneSwingTwists = new Vector3[skeleton.bones.Length];
		shapes = new Dictionary<string, float>();
		tileCount = new Vector2Int(width/tileLen, height);
		tilePow = (int)System.Math.Pow(tileRadix, tileLen*3);
	}

	const float PositionScale = 2;
	private NativeArray<Color> tex = new NativeArray<Color>();
	private Vector2Int texSize, tileCount;
	private int tilePow;
	float SampleTile(int idx) {
		int x = idx / tileCount.y;
		int y = idx % tileCount.y;
		x += layer/2 * 3;
		if((layer & 1) != 0)
			x = tileCount.x-1-x;

		x *= texSize.x/tileCount.x;
		y *= texSize.y/tileCount.y;
		return tex[x + (texSize.y-1-y) * texSize.x].r;
	}	
	public void Update(AsyncGPUReadbackRequest req) {
		tex = req.GetData<Color>();
		texSize = new Vector2Int(req.width, req.height);

		var hipsTT = new float[6];
		var hipsY = Vector3.up;
		var hipsZ = Vector3.forward;
		Array.Clear(boneSwingTwists, 0, boneSwingTwists.Length);
		for(int i=0; i<skeleton.bones.Length; i++) {
			var slot = layout.baseIndices[i];
			foreach(var j in layout.channels[i]) {
				var v = SampleTile(slot);
				if(j<3)
					boneSwingTwists[i][j] = v * 180;
				else if(j<9)
					hipsTT[j-3] = v;
				else if(j<12)
					hipsY[j-9] = v;
				else if(j<15)
					hipsZ[j-12] = v;

				slot++;
			}
		}
		for(int i=0; i<3; i++)
			hipsT[i] = ShaderImpl.DecodeVideoFloat(hipsTT[i], hipsTT[i+3], tilePow);
		(hipsY, hipsZ) = ShaderImpl.orthogonalize(hipsY, hipsZ);
		hipsT *= PositionScale;
		if(hipsZ.magnitude > 0) {
			hipsQ = Quaternion.LookRotation(hipsZ, hipsY);
			hipsS = hipsY.magnitude / hipsZ.magnitude;
		} else {
			hipsQ = Quaternion.identity;
			hipsS = 1;
		}

		shapes.Clear();
		foreach(var si in layout.shapeIndices) {
			float w;
			shapes.TryGetValue(si.shape, out w);
			shapes[si.shape] = w + SampleTile(si.index) * si.weight;
		}
	}
	private HumanPoseHandler poseHandler;
	private HumanPose humanPose;
	public void ApplyHumanPose() {
		if(poseHandler == null) {
			poseHandler = new HumanPoseHandler(skeleton.root.GetComponent<Animator>().avatar, skeleton.root);
			poseHandler.GetHumanPose(ref humanPose);
		}
		CalcHumanPose(ref humanPose);
		poseHandler.SetHumanPose(ref humanPose);
	}
	public void ApplyTransform() {
		for(int i=0; i<skeleton.bones.Length; i++)
			if(skeleton.bones[i]) {
				var axes = skeleton.axes[i];
				if(i != (int)HumanBodyBones.Hips) {
					var rot = axes.preQ * BoneAxes.SwingTwist(axes.sign * boneSwingTwists[i])
												* Quaternion.Inverse(axes.postQ);
					if(!skeleton.dummy[i]) // TODO: merge rotation
						skeleton.bones[i].localRotation = rot;
				}
				else {
					var rescale = hipsS / skeleton.scale;
					skeleton.root.localScale = new Vector3(1,1,1) * rescale;
					skeleton.bones[i].SetPositionAndRotation(
									skeleton.root.TransformPoint(hipsT / rescale),
									skeleton.root.rotation * hipsQ * Quaternion.Inverse(axes.postQ));
				}
			}
	}
	public void ApplyBlendShape() {
		if(shapeRenderer) {
			var mesh = shapeRenderer.sharedMesh;
			foreach(var kv in shapes) {
				var shape = mesh.GetBlendShapeIndex(kv.Key);
				var frame = mesh.GetBlendShapeFrameCount(shape)-1;
				var weight = mesh.GetBlendShapeFrameWeight(shape, frame);
				shapeRenderer.SetBlendShapeWeight(shape, kv.Value * weight);
			}
		}
	}

	private void CalcHumanPose(ref HumanPose pose) {
		Array.Clear(pose.muscles, 0, pose.muscles.Length);
		for(int i=0; i<HumanTrait.BoneCount; i++)
			for(int j=0; j<3; j++) {
				var (muscle, weight) = boneMuscles[i, j];
				if(muscle >= 0)
					pose.muscles[muscle] += boneSwingTwists[i][j] * weight;
			}
		for(int i=0; i<HumanTrait.MuscleCount; i++)
			pose.muscles[i] /= pose.muscles[i] >= 0 ? muscleLimits[i,1] : -muscleLimits[i,0];

		HumanPoser.SetHipsPositionRotation(ref pose, hipsT / hipsS, hipsQ);
	}

	public static readonly (int, float)[,] boneMuscles;
	public static readonly float[,] muscleLimits;
	static BonePlayer() {
		boneMuscles = new (int, float)[HumanTrait.BoneCount, 3];
		for(int i=0; i<HumanTrait.BoneCount; i++) 
			for(int j=0; j<3; j++) {
				var ii = i;
				var jj = j;
				var muscle = HumanTrait.MuscleFromBone(ii, jj);
				var weight = (float)1;
				if(muscle < 0) {
					switch(ii) {
					case (int)HumanBodyBones.LeftShoulder:
						ii = (int)HumanBodyBones.LeftUpperArm; break;
					case (int)HumanBodyBones.RightShoulder:
						ii = (int)HumanBodyBones.RightUpperArm; break;
					case (int)HumanBodyBones.Jaw:
						break;
					case (int)HumanBodyBones.LeftLowerArm:
					case (int)HumanBodyBones.RightLowerArm:
						weight = -1;
						jj = 0;
						goto default;
					case (int)HumanBodyBones.LeftLowerLeg:
					case (int)HumanBodyBones.RightLowerLeg:
						jj = 0;
						goto default;
					default:
						ii = HumanTrait.GetParentBone(ii);break;
					}
					muscle = HumanTrait.MuscleFromBone(ii, jj);
				}
				boneMuscles[i, j] = (muscle, weight);
			}
		muscleLimits = new float[HumanTrait.MuscleCount, 2];
		for(int i=0; i<HumanTrait.MuscleCount; i++) {
			muscleLimits[i, 0] = HumanTrait.GetMuscleDefaultMin(i);
			muscleLimits[i, 1] = HumanTrait.GetMuscleDefaultMax(i);
		}
	}
}
}