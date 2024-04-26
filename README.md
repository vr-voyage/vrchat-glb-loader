# Starting to work well

A Udon-ready GLTF Binary (GLB) loader.

## Download

[Download the latest release here](https://github.com/vr-voyage/vrchat-glb-loader/releases/latest)

## Demo video

[out.mp4](https://github.com/vr-voyage/vrchat-glb-loader/assets/84687350/d2b4e901-8e4d-4823-acd9-ce4d210acc25)

Showcased assets :
* **by__Rx**'s [Salty Snack | Firearm | Game Ready](https://sketchfab.com/3d-models/salty-snack-firearm-game-ready-702411980d904abc974efef9ba4e47d5)
* **carlcapu9**'s [Post Apocalyptic Office](https://sketchfab.com/3d-models/post-apocalyptic-office-ace3403d966d4201b1a376cfdcca7a5a)

## Test world

You can test the loader in the following world :

https://vrchat.com/home/launch?worldId=wrld_a74abb7d-a423-44bb-a7ea-3bc5e8281dde

## Use-case

The point is to be able to load GLB data in-game.

This can handle complex scenes, however a lot of features are still missing.

Still, you can give it a try.
To load the textures, you need to make sure they're converted to DDS before hand.
I prepared [a converter to do that](https://github.com/vr-voyage/glb-textures-converter-rust/releases/latest).

The reason I need to pre-convert the textures is because
[LoadImage](https://docs.unity3d.com/ScriptReference/ImageConversion.LoadImage.html)
doesn't seem to be exposed in U#.

Also, no bones or armature support yet.

