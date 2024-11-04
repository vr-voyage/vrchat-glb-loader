using System.Collections;
using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using VoyageVoyage;
using VRC.SDK3.Data;

public abstract class MaterialExtensionHandler : UdonSharpBehaviour
{
    public void HandleMaterial(
        Material material,
        DataDictionary extensionDefinition,
        DataDictionary mainMaterialDefinition,
        GLBLoader loader)
    {
        HandleInternal(material, extensionDefinition, mainMaterialDefinition, loader);
        loader.SendCustomEvent("ExtensionDone");
    }

    abstract public void HandleInternal(
        Material material,
        DataDictionary extensionDefinition,
        DataDictionary mainMaterialDefinition,
        GLBLoader loader);

    abstract public string HandledExtensionName();
}
