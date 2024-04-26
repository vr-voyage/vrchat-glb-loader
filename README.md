# It's starting to work pretty well

A Udon-ready GLTF Binary (GLB) loader.

[out-demo.webm](https://github.com/vr-voyage/vrchat-glb-loader/assets/84687350/001c47a1-278e-4bae-99f5-a0db48d7c3bc)

The point is to be able to load GLB data in-game.

This is now handling complex scenes. There's still a lot of
cases that are not tested, but this can load GLB models from
Sketchfab, for example, as long as you preconvert the textures before.

A converter is available here : 

The pre-conversion just ensure that the textures are using
a GPU-ready format like DXT5 or BC7.  
The reason being that I still have no idea on how I could convert
JPEG or PNG data to a texture using U#, since LoadImage is not whitelisted.
