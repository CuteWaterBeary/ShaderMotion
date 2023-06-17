# ShaderMotion

**The audience plays the video stream** in different+any+multiple VRChat worlds, and **watches a 3D avatar puppet following the encoded motion**.

This toolset can be used to create 3D Movies and Performances in which the audience can move around the stage/world to experience it from any angle.

Amother use case is music performances in VRChat that can be viewed in VR without worring about instance limits on worlds because if the encoded motion video can be played in many instances at once allowing thousands of people to experience it at once.


### A shader-based motion-to-video codec for humanoid avatars

ShaderMotion is a motion-to-video codec for Unity humanoid avatar, whose core system is completely written in shader language HLSL.

It is designed for streaming fullbody motion across VR platforms using popular live streaming platforms.

The **sender converts bone rotations of a humanoid avatar into video color blocks** for transmission. The receiver converts color blocks back to bone rotations for motion playback, **and allows the motion to be retargeted to a different avatar**.


### [>> Click here for the web demo <<](https://lox9973.com/ShaderMotion/)

This project has been partially ported to WebGL2. Audience can watch in desktop browser, and even export streamer's motion into Unity animation file.
om a previous version, please remove the folder before extraction.

## Getting started

This project contains a working example scene `Example/Example.unity` which gives an overview of the whole system.


### VRC_Avatar_Addon

VRC_Avatar_Addon generates a ShaderMotion recorder/player and sets up animator controller on a VRChat Avatar. Avatar 3.0 is recommended, while Avatar 2.0 support is limited.

- Select your avatar (the gameObject with a VRCAvatarDescriptor component).
- In Animator component tab, click the gear and choose "SetupAvatar".
- (Optional) Click "Setup Motion Recorder". It adds a SkinnedMeshRenderer as motion recorder.
- (Optional) Click "Setup Motion Player". It adds a MeshRenderer as motion player (MotionLink).
- Click "Setup Animator". For Avatar 3.0, FX layer and expressions will be modified. For Avatar 2.0, override controller will be modified.

### VRC_World_Addon

VRC_World_Addon provides a sample VRCSDK3 world with a video player which drives a puppet avatar.



![Overview](/GameView.png)


# Install

Copy this project into your Assets folder in Unity.

## Create a shader recorder

Select your avatar root (the GameObject with humanoid Animator), click the gear icon in Animator inspector, and click menu item `CreateMeshRecorder`. The recorder will be created as a SkinnedMeshRenderer, which outputs motion data to screen. The **motion data is currently a 6x45 grid of colored blocks on the left or right side of the screen.** The material property "Layer" (0~11) determines it's position. By default the motion data is set to be visible only in **orthographic camera with farClip = 0** in order to reduce interference.

The `rootBone` property of this SkinnedMeshRenderer determines the origin of recording space.

It's set to be the recorder by default but it can be any Transform. You may add an emote/gesture toggle to keep it fixed in world space, so that your locomotion is recorded.

## Create a shader player

Select the main renderer (typically a SkinnedMeshRenderer called Body) in your avatar, click the gear icon in Animator inspector, and click menu item `CreateMeshPlayer`. The player will be created as a SkinnedMeshRenderer, which takes motion data from `Motion.renderTexture` by default.

## Create a C# player

There is a MonoBehaviour called MotionPlayer which can drive an animator from the motion texture. Unlike shader player, it requires custom script and won't work in VRChat. The use of c# player is to pair with animation recorder below to export motion as `.anim` file.

## Animation recorder

You can export avatar motion to AnimationClip in play mode. This tool can be found in Animator gear menu `RecordAnimation`.

## Recommended OBS streaming settings

If you plan to stream your motion so that others can view in other worlds, please follow the recommended settings:

**Video**
* Output resolution: at least 640x360
* FPS: 60. _Lower fps is very visible in VR_

**Output/Streaming**
* Bitrate: 400 ~ 800 Kbps.
Scale the bitrate properly if your resolution or FPS is larger than the minimum recommended settings above.

**Advanced/Video**
* Color Space: 709
* Color Range: Partial


## Bone orientation

Unity calibrates bone orientations when an avatar is imported.

In most cases, x-axis is used for twist (axial motion) and yz-axes are used for swing (spherical motion).

Its exact values can be accessed by Unity internal methods `Avatar.GetPostRotation` and `Avatar.GetLimitSign`, and **they do not match the axes of the bone Transform**.

For reference, we provide a table of calibrated bone orientations when the avatar is in T-pose (facing forward), with Unity's axes convention +X = right, +Y = up, +Z = forward.

| Bone                         | x-axis | y-axis | z-axis | Bone                | x-axis | y-axis | z-axis |
|:-----------------------------|:-------|:-------|:-------|:--------------------|:-------|:-------|:-------|
| Hips                         | +X     | +Y     | +Z     | Left UpperLeg/Foot  | -Y     | -Z     | +X     |
| Spine/(Upper)Chest/Neck/Head | +Y     | -Z     | -X     | Left LowerLeg       | -Y     | +Z     | -X     |
| LeftEye                      |        | -Y     | -X     | Right UpperLeg/Foot | +Y     | +Z     | +X     |
| RightEye                     |        | +Y     | -X     | Right LowerLeg      | +Y     | -Z     | -X     |
| Left Shoulder/UpperArm/Hand  | -X     | -Y     | -Z     | Left Thumb          |        | -X-Z   | +Y     |
| Left LowerArm                | -X     | +Z     | -Y     | Left Index/Middle   |        | +Y     | -Z     |
| Right Shoulder/UpperArm/Hand | -X     | +Y     | +Z     | Left Ring/Little    |        | -Y     | -Z     |
| Right LowerArm               | -X     | -Z     | +Y     | Right Thumb         |        | -X+Z   | -Y     |
| Jaw                          |        | TBD    | TBD    | Right Index/Middle  |        | -Y     | +Z     |
| Left/Right Toes              |        |        | +X     | Right Ring/Little   |        | +Y     | +Z     |

**There are a few things worth noting in the table.**
* Some entries are omitted because the bones can't rotate in certain axes.
* Finger orientations apply to all three bones Proximal/Intermediate/Distal.
* Axes for right limbs can be derived from left ones by flipping signs of ±Y and ±Z.
* X-axis may be the opposite of bone direction, because rotation sign is baked into it.
* Some axes implicitly used by Mecanim have signs flipped to match handedness of adjacent bones.
* Hips' orientation matches rootQ in Unity and is not intended for swing-twist.

## Bone rotation

Bone rotation is computed from its neutral pose relative to its parent bone.
The neutral pose, aka motorcycle pose, can be seen in avatar's muscle settings in Unity, and accessed by Unity internal method `Avatar.GetPreRotation`.
The rotation is **expressed by swing-twist angles**, instead of euler angles or quaternion, for compatibility with Unity's muscle system and easier interpolation.

**Here is C# code for computing bone rotation, assuming calibrated orientation.**

```
boneLocalRotation = boneLocalNeutralRotation * SwingTwist(angles);
Quaternion SwingTwist(Vector3 angles) {
	var anglesYZ = new Vector3(0, angles.y, angles.z);
	return Quaternion.AngleAxis(anglesYZ.magnitude, anglesYZ.normalized)
		* Quaternion.AngleAxis(angles.x, new Vector3(1, 0, 0));
}
```

**The resulting swing-twist angles should match Unity animator's muscle values up to scaling.**

For example, the yz-angles of the bone `Left Thumb Proximal` should be more or less the muscle values of `Left Thumb Spread` & `Left Thumb 1 Stretched` multiplied by the bone's range limit.

However, this is only an *approximation* due to the complex behavior like twist distribution in Mecanim.



## Overview

This section discusses how to encode a bounded real number into RGB colors.
All RGB colors are assumed to be sRGB with gamma correction, with each component in [0,1].

We assume the real number is normalized in [0,1] without loss of generality.
Then the encoding is simply a function from [0,1] to [0,1]ⁿ, where n is 3 times the number of colors.

We require the function to be continuous to avoid jitter from quantization.

As a result, the goal is to **find a continuous curve in n-dimensional cube**.

## Theory

The encoding curve should behave like a space-filling curve to maximize coding efficiency.

We choose **base-3 Gray curve** to be the encoding curve, after experimenting with other bases like 2,4 and other curves like Hilbert curve.

A *base-3 Gray curve* is obtained by connecting adjacent points in base-3 Gray code.

For example, **when n=2, Gray code is a function from {0, .., 8} to {0,1,2}².**

```
0 -> 00, 1 -> 01, 2 -> 02
3 -> 12, 4 -> 11, 5 -> 10
6 -> 20, 7 -> 21, 8 -> 22
```

After scaling the domain and range into [0,1] and [0,1]² and linear interpolation, it becomes a continuous function from [0,1] to [0,1]².

## Practice

The frame layout section needs to encode a real number between ±1 into two RGB colors.

**Here is the encoding algorithm**

1. Apply the function `x ↦ (x+1)/2` to normalize the input number from [-1,+1] to [0,1].
2. Apply the encoding curve with n=6 to get 6 numbers in [0,1].
3. Interpret the 6 numbers as two RGB colors in the order of GRBGRB.

Note the order GRB is chosen to maximize coding efficiency, because H.264 encodes in YCrCb space where G-axis is the longest.

There is no specification of the decoding algorithm, other than it being the left inverse of the encoding algorithm.

However, custom implementation is strongly recommended to decode by finding the nearest curve point.

## Integral & fraction parts

TBD



## Frame layout

The frame is divided into a 80×45 grid of squares.

Each square has a power-of-two size under normal video resolution, and it is filled with a single color when the data exists.

Horizontally adjacent squares are paired into a *slot*, which encodes a real number between ±1 (encoding scheme will be introduced in later sections).

As a result, **each frame represents a 40×45 matrix of real numbers between ±1**.

The slots are indexed from top to down, left to right, starting from 0. For example, slots in the first three columns are `0~44, 45~89, 90~134`.

Currently, a humanoid avatar occupies the first three columns.

| Slot  | Use                  | Slot  | Use           | Slot    | Use         |
|:------|:---------------------|:------|:--------------|:--------|:------------|
| 0~2   | Hips (position high) | 45~47 | LeftShoulder  | 90~93   | LeftThumb   |
| 3~5   | Hips (position low)  | 48~50 | RightShoulder | 94~97   | LeftIndex   |
| 6~8   | Hips (scaled y-axis) | 51~53 | LeftUpperArm  | 98~101  | LeftMiddle  |
| 9~11  | Hips (scaled z-axis) | 54~56 | RightUpperArm | 102~105 | LeftRing    |
| 12~14 | Spine                | 57~59 | LeftLowerArm  | 106~109 | LeftLittle  |
| 15~17 | Chest                | 60~62 | RightLowerArm | 110~113 | RightThumb  |
| 18~20 | UpperChest           | 63~65 | LeftHand      | 114~117 | RightIndex  |
| 21~23 | Neck                 | 66~68 | RightHand     | 118~121 | RightMiddle |
| 24~26 | Head                 | 69    | LeftToes      | 122~125 | RightRing   |
| 27~29 | LeftUpperLeg         | 70    | RightToes     | 126~129 | RightLittle |
| 30~32 | RightUpperLeg        | 71~72 | LeftEye       |         |             |
| 33~35 | LeftLowerLeg         | 73~74 | RightEye      |         |             |
| 36~38 | RightLowerLeg        | 75~76 | ~~Jaw~~ (deprecated) |         |             |
| 39~41 | LeftFoot             |       |               |         |             |
| 42~44 | RightFoot            |       |               |         |             |

Most slots store swing-twist angles, in the order of XYZ, scaled from `[-180°, +180°]` to `[-1, +1]`.
* Finger angles are stored in the order of Proximal YZ, Intermediate Z, Distal Z.
* LeftEye/RightEye angles are stored in the order of YZ.
* LeftToes/RightToes have only Z angles.

## Special handling of Hips

Hips bone is an exception because it can translate and rotate freely, relative to the origin of recording space. It also need to encode _avatar scale_ (the height of Hips bone in T-pose) for retargeting.

Its position is scaled down by a factor of 2, encoded into two parts which approximate integral & fractional parts (see encoding scheme section), and put separately into slot `0~2` and `3~5`.

The scaling factor is chosen so that the normal range of motion will have integral part equal zero, which makes possible for sloppy decoders to skip decoding the integral part.

Its rotation is represented by rotation matrix to avoid discontinuity in swing-twist or quaternion representation.

Slot `6~8` and `9~11` store the second and third column of the rotation matrix, i.e. its y-axis and z-axis, scaled appropriately so that `length(scaled y-axis)/length(scaled z-axis)` equals the avatar scale.
