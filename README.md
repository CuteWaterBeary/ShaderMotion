# ShaderMotion

## A shader-based motion-to-video codec for humanoid avatar

ShaderMotion is a motion-to-video codec for Unity humanoid avatar, whose core system is completely written in shader language HLSL. It is designed for streaming fullbody motion across VR platforms using popular live streaming platforms. The sender converts bone rotations of a humanoid avatar into video color blocks for transmission. The receiver converts color blocks back to bone rotations for motion playback, and allows the motion to be retargeted to a different avatar.

This project started as an attempt to stream VR user motion across VRChat instances, inspired by [memex's performance "Omnipresence Live"](http://meme-x.jp/2020/05/omnipresencelive/). A performer using a special avatar streams their screen with color blocks to a live streaming platform like Twitch. The audience plays the video stream in a different VRChat world, and watches a 3D avatar puppet following the encoded motion.

## [>> Click here for the web demo <<](https://lox9973.com/ShaderMotion/)

This project has been partially ported to WebGL2. Audience can watch in desktop browser, and even export streamer's motion into Unity animation file.

## Installation

- Unity 2018+ is required (tested on Unity 2018.4.20f1).
- Download the latest [release](../../releases) zip file.
- Extract the zip file into `Assets/ShaderMotion` in your Unity project. If you are upgrading from a previous version, please remove the folder before extraction.

## Getting started

This project contains a working example scene `Example/Example.unity` which gives an overview of the whole system. If you are interested in technical details, please read the [wiki](../../wikis/home).

![Overview](../../wikis/uploads/f6c3a9855edf0b8ee69a37bdfe3aff07/GameView.png)
