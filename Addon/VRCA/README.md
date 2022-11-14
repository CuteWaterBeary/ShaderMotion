# Addon: VRChat Avatar

This addon generates a ShaderMotion recorder/player and sets up animator controller on a VRChat Avatar. Avatar 3.0 is recommended, while Avatar 2.0 support is limited.

## Getting started

- Select your avatar (the gameObject with a VRCAvatarDescriptor component).
- In Animator component tab, click the gear and choose "SetupAvatar".
- (Optional) Click "Setup Motion Recorder". It adds a SkinnedMeshRenderer as motion recorder.
- (Optional) Click "Setup Motion Player". It adds a MeshRenderer as motion player (MotionLink).
- Click "Setup Animator". For Avatar 3.0, FX layer and expressions will be modified. For Avatar 2.0, override controller will be modified.