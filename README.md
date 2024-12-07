# About

![rect66626](https://github.com/user-attachments/assets/91a1a71b-bc9b-412b-8b1d-8eee81eb0db1)

This 3D Model Loader can download and reconstruct 3D Models inside a VRChat World.
GLB and VRM are supported.

This reconstructs :
* Meshes
* Materials
* Textures (if pre-converted, see https://feedback.vrchat.com/udon/p/whitelist-imageconversionloadimage-in-udon )
* Scenes

The following are not supported yet :
* Bones ( See https://feedback.vrchat.com/udon/p/whitelist-unityengineboneweight-in-udon )
* Armatures
* Animations
* Blendshapes
* Camera
* Lights

## Download

[Download the latest release here](https://github.com/vr-voyage/vrchat-glb-loader/releases/latest)

## Demo video

### GLB (Using Blender)

[demo-vrchat-blender.webm](https://github.com/user-attachments/assets/7937a0db-808a-4735-b7de-033ae9986b31)

This uses the following add-on was used to preconvert the textures :
https://github.com/vr-voyage/blender-glb-extension-gpu-formats

### VRM (Using VRoid Studio)

[demo-vrchat-vrm-tex-converter.webm](https://github.com/user-attachments/assets/de6c966e-88fb-4f49-b5df-03273d016dea)

This uses the following software to preconvert the textures
https://github.com/vr-voyage/glb-textures-converter-rust

### Previous demo

[out.mp4](https://github.com/vr-voyage/vrchat-glb-loader/assets/84687350/d2b4e901-8e4d-4823-acd9-ce4d210acc25)

Showcased assets :
* **by__Rx**'s [Salty Snack | Firearm | Game Ready](https://sketchfab.com/3d-models/salty-snack-firearm-game-ready-702411980d904abc974efef9ba4e47d5)
* **carlcapu9**'s [Post Apocalyptic Office](https://sketchfab.com/3d-models/post-apocalyptic-office-ace3403d966d4201b1a376cfdcca7a5a)

## Test world

You can test the loader in the following world :

https://vrchat.com/home/launch?worldId=wrld_a74abb7d-a423-44bb-a7ea-3bc5e8281dde

## Limitations

### Textures need to be preconverted

https://feedback.vrchat.com/udon/p/whitelist-imageconversionloadimage-in-udon

The method LoadImage being unavailable through Udon, I need to preconvert the textures in advance to be able to use this

### Bones are not supported

https://feedback.vrchat.com/udon/p/whitelist-unityengineboneweight-in-udon

Udon prevents access to the members of the BoneWeight structure. Without this structure, it is impossible to represent the bones weight, making the whole armature support impossible through standard means.

