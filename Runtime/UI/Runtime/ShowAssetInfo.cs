
using UdonSharp;
using VoyageVoyage;

public class ShowAssetInfo : UdonSharpBehaviour
{
    public GLBLoader loader;

    public TMPro.TextMeshProUGUI copyright;
    public TMPro.TextMeshProUGUI generator;
    public TMPro.TextMeshProUGUI version;

    public void SceneLoaded()
    {
        if (loader == null) { return; }
        var assetInfoObject = loader.assetInfoObject;
        if (assetInfoObject == null) { return; }
        copyright.text = loader.assetInfoObject.copyright;
        generator.text = loader.assetInfoObject.generator;
        version.text = loader.assetInfoObject.version;
    }
}
