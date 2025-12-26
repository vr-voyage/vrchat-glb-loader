
using UdonSharp;
using VoyageVoyage;

public class ShowAssetInfo : UdonSharpBehaviour
{
    public GLBLoader loader;

    public TMPro.TextMeshProUGUI copyright;
    public TMPro.TextMeshProUGUI generator;
    public TMPro.TextMeshProUGUI assetName;

    public TMPro.TextMeshProUGUI triangles;
    public TMPro.TextMeshProUGUI images;
    public TMPro.TextMeshProUGUI materials;

    public void SceneLoaded()
    {
        if (loader == null) { return; }
        if (copyright == null || generator == null) { return; }
        var assetInfoObject = loader.assetInfoObject;
        if (assetInfoObject == null) { return; }
        copyright.text = assetInfoObject.copyright;
        generator.text = assetInfoObject.generator;

        if (assetName == null || triangles == null || images == null || materials == null) { return; }

        assetName.text = assetInfoObject.assetName;
        triangles.text = loader.GetTrianglesCount().ToString();
        images.text = loader.GetImagesCount().ToString();
        materials.text = loader.GetMaterialsCount().ToString();
        
    }
}
