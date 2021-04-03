# ShaderMotion ([click here for WebGL2 demo](https://lox9973.com/ShaderMotion/))

## A shader-based humanoid motion codec and avatar puppeteer 

ShaderMotion is a humanoid motion codec and avatar puppeteer for Unity 2018, whose codec and skinning system is completely written in shader language HLSL. It is designed for streaming 3d motion across VR platforms using popular live streaming platforms. The sender takes an avatar model and encodes its bone rotations to color pixels in video. The receiver takes color pixels from video and animates an avatar using the encoded bone rotations. It's able to handle animation retargeting, loosely based on [Unity Mecanim](https://blogs.unity3d.com/2014/05/26/mecanim-humanoids/).

This project starts as an attempt to stream 3d motion across VRChat instances, inspired by [memex's "Omnipresence Live"](http://meme-x.jp/2020/05/omnipresencelive/). The streamer wearing a special avatar broadcasts their screen with encoded motion to a live streaming platform like Twitch. The audience can watch the stream in another VRChat world with video player and motion player, and view a 3d avatar following the streamer's motion in the 2d video.

This project is partially ported to WebGL2 (link above). Audience can watch in desktop browser, and even export streamer's move as Unity animation file now!

Please read [Wiki](../../wikis/home) for technical details.

## Requirements

- Unity 2018+ (tested on Unity 2018.4.20f1)

It doesn't depend on VRCSDK.

## Installation

- Download the latest [release](../releases) zip file.
- Extract zip file into `Assets/ShaderMotion` in your Unity project. If you are upgrading from a previous version, please remove the folder before extraction.

## Getting started

Check out the example scene under `Example` folder for instruction.

![Overview](../../wikis/uploads/f6c3a9855edf0b8ee69a37bdfe3aff07/GameView.png)
