# ShaderMotion

This project implements a shader-based human motion encoder/decoder in Unity 2018. The encoder takes one avatar and encodes approximated Unity muscle values to a texture. The motion decoder takes the texture and animates another avatar using the decoded muscle values. The encoder/decoder requires mesh data pre-generated in editor script, but the encoding/decoding process is completely done in shader.

This project is intended for streaming 3d motion in VRChat. The streamer uses an avatar with encoder in any world, and broadcasts their screen with encoded motion. The viewers can play the motion video in a map with video player, and view the streamer's motion through a decoder, which can be included in the map or even in the viewer's own avatar!

# Get Started

Copy this project folder into Assets folder in your Unity project, and open the example scene.

Please read [Wiki](../../wikis/home) for further explanation. The following is an overview for the dev branch.

![Overview](../../wikis/uploads/5991285fe23b59df8140d30a19683614/GameView.png)
