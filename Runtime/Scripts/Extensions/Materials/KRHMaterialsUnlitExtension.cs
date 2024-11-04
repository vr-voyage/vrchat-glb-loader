
using UdonSharp;
using UnityEngine;
using VoyageVoyage;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

public class KRHMaterialsUnlitExtension : MaterialExtensionHandler
{
    
    public Material templateUnlit;
    public Material defaultLitTemplate;

    public override string HandledExtensionName()
    {
        return "KHR_materials_unlit";
    }

    public override void HandleInternal(Material material, DataDictionary extensionDefinition, DataDictionary mainMaterialDefinition, GLBLoader loader)
    {
        if (material.shader.name == defaultLitTemplate.shader.name)
        {
            material.shader = templateUnlit.shader;
        }
    }
}
