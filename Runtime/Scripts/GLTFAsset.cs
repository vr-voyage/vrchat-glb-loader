
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class GLTFAsset : UdonSharpBehaviour
{
    public string version = "";
    public string copyright = "";
    public string generator = "";
    public string minVersion = "";
    public string assetName = "";

    public void Clear()
    {
        version = "";
        copyright = "";
        generator = "";
        minVersion = "";
        assetName = "";
    }

}
