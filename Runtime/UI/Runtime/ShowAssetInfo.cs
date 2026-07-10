
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

    void SetText(TMPro.TextMeshProUGUI gui, string text)
    {
        if (gui != null && text != null) gui.text = text;
    }

    void Clear()
    {
        SetText(copyright, "");
        SetText(generator, "");
        SetText(assetName, "");
        SetText(triangles, "");
        SetText(images, "");
        SetText(materials, "");
    }

    public void SceneLoaded()
    {
        if (loader == null) { return; }
        var assetInfoObject = loader.assetInfoObject;
        if (assetInfoObject == null) { return; }

        gameObject.SetActive(true);

        SetText(copyright, assetInfoObject.copyright);
        SetText(generator, assetInfoObject.generator);

        SetText(assetName, assetInfoObject.assetName);
        SetText(triangles, loader.GetTrianglesCount().ToString());
        SetText(images, loader.GetImagesCount().ToString());
        SetText(materials, loader.GetMaterialsCount().ToString());
        
    }

    public void LoadingScene()
    {
        Clear();
        gameObject.SetActive(false);
    }
}
