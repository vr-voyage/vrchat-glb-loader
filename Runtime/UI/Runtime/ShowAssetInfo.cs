
using UdonSharp;
using UnityEngine;
using VoyageVoyage;
using VRC.SDKBase;
using VRC.Udon;

public class ShowAssetInfo : UdonSharpBehaviour
{
    public GLBLoader loader;

    public TMPro.TextMeshProUGUI copyright;
    public TMPro.TextMeshProUGUI generator;
    public TMPro.TextMeshProUGUI version;

    public void SceneLoaded()
    {
        copyright.text = loader.assetInfoObject.copyright;
        generator.text = loader.assetInfoObject.generator;
        version.text = loader.assetInfoObject.version;
    }
}
