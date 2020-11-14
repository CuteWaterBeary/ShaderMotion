[Click here to play with the demo](https://lox9973.com/ShaderMotion/) (WebGL2 required)

# ShaderMotion

This project implements a shader-based human pose encoder/decoder in Unity 2018. The encoder takes an avatar model and encodes its (approximated) Unity muscle values to an image. The decoder takes an image and animates a (possibly different) avatar model with the muscle values encoded in the image. The encoder/decoder requires mesh data pre-generated in Unity editor script, but the encoding/decoding process is completely done in shader.

This project starts as an attempt of streaming 3d motion smoothly from VRChat, inspired by [memex's "Omnipresence Live"](http://meme-x.jp/2020/05/omnipresencelive/). The streamer should use an avatar with encoder, and broadcast their screen with encoded motion to a live streaming platform like Twitch. The audience can watch the stream in any VRChat world with video player and motion decoder, and view a 3d avatar following the streamer's motion in the 2d video.

The motion decoder is also ported to WebGL2 (link above) so that audience can watch in browser and even export streamer's move as Unity animation file!

# Get Started

Copy this project folder into Assets folder in your Unity project, and open the example scene for instruction.

Please read [Wiki](../../wikis/home) for technical details.

![Overview](../../wikis/uploads/5991285fe23b59df8140d30a19683614/GameView.png)