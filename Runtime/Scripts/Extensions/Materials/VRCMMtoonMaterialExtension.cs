using System;
using System.CodeDom;
using UnityEngine;
using UnityEngine.Rendering;
using VoyageVoyage;
using VRC.SDK3.Data;


public class VRCMMtoonMaterialExtension : MaterialExtensionHandler
{
    const int boolType = 1;
    const int intType = 2;
    const int floatType = 3;
    const int rgbType = 4;
    const int gltfTextureType = 5;
    const int floatEnumType = 6;

    const int gltfPropertyNameIndex = 0;
    const int unityShaderKeywordsIndex = 1;
    const int unityPropertyIndex = 2;
    const int shaderPropertyTypeIndex = 3;
    const int defaultValueIndex = 4;
    const int acceptableValuesRangeIndex = 5;

    Vector2 defaultOffset = Vector2.zero;
    Vector2 defaultScale = Vector2.one;

    public Material templateMaterial;

    object[] mToonProperties =
    {
        new object[] {"transparentWithZWrite", null, "_TransparentWithZWrite", boolType, false},
        new object[] {"renderQueueOffsetNumber", null, "_RenderQueueOffset", floatType, 0, new object[] { -9, 9 } },
        new object[] {"shadeColorFactor", null, "_ShadeColor", rgbType, new Color(1,1,1,1)},
        new object[] {"shadeMultiplyTexture", null, "_ShadeTex", gltfTextureType},
        new object[] {"shadingShiftFactor", new string[] { "_MTOON_PARAMETERMAP" }, "_ShadingShiftFactor", floatType, 0.0},
        new object[] {"shadingToonyFactor", null, "_ShadingToonyFactor", floatType, 0.9, new object[] {0, 1}},
        new object[] {"giEqualizationFactor", null, "_GiEqualization", floatType, 0.9, new object[] {0, 1}},
        new object[] {"matcapFactor", null, "_MatcapColor", rgbType, new Color(0,0,0,1) },
        new object[] {"matcapTexture", new string[] { "_MTOON_RIMMAP" }, "_MatcapTex", gltfTextureType},
        new object[] {"parametricRimColorFactor", null, "_RimColor", rgbType, new Color(0,0,0,1) },
        new object[] {"rimMultiplyTexture", new string[] { "_MTOON_RIMMAP" }, "_RimTex", gltfTextureType},
        new object[] {"rimLightingMixFactor", null, "_RimLightingMix", floatType, 1.0, new object[] {0,1}},
        new object[] {"parametricRimFresnelPowerFactor", null, "_RimFresnelPower", floatType, 5.0, new object[] { 0 }},
        new object[] {"parametricRimLiftFactor", null, "_RimLift", floatType, 0},
        new object[] {"outlineWidthMode", null, "_OutlineWidthMode", floatEnumType, "none", new string[] {"none", "worldCoordinates", "screenCoordinates"}},
        new object[] {"outlineWidthFactor", null, "_OutlineWidth", floatType, 0, new object[] { 0 }},
        new object[] {"outlineWidthMultiplyTexture", new string[] { "_MTOON_PARAMETERMAP" }, "_OutlineWidthTex", gltfTextureType},
        new object[] {"outlineColorFactor", null, "_OutlineColor", rgbType, new Color(0,0,0,1) },
        new object[] {"outlineLightingMixFactor", null, "_OutlineLightingMix", floatType, 1, new object[] {0,1}},
        new object[] {"uvAnimationMaskTexture", new string[] { "_MTOON_PARAMETERMAP" }, "_UvAnimMaskTex", gltfTextureType},
        new object[] {"uvAnimationScrollXSpeedFactor", null, "_UvAnimScrollXSpeed", floatType, 0},
        new object[] {"uvAnimationScrollYSpeedFactor", null, "_UvAnimScrollYSpeed", floatType, 0},
        new object[] {"uvAnimationRotationSpeedFactor", null, "_UvAnimRotationSpeed", floatType, 0}
    };

    bool DoesPropertyContainsDefault(object[] propertyInfo)
    {
        return propertyInfo.Length > defaultValueIndex;
    }

    bool DoesPropertyContainsValuesRange(object[] propertyInfo)
    {
        return propertyInfo.Length > acceptableValuesRangeIndex;
    }

    void SetMaterialFloat(Material material, string propertyName, float value)
    {
        material.SetFloat(propertyName, value);
    }

    void ApplyDefaultFloatValue(Material material, string propertyName, object defaultValue)
    {
        if (defaultValue != null) { SetMaterialFloat(material, propertyName, (float)defaultValue); }
    }

    void SetMaterialInt(Material material, string propertyName, int value)
    {
        material.SetInt(propertyName, value);
    }

    void ApplyDefaultIntValue(Material material, string propertyName, object defaultValue)
    {
        if (defaultValue != null) { SetMaterialInt(material, propertyName, (int)defaultValue); }
    }

    void SetMaterialColor(Material material, string propertyName, Color color)
    {
        material.SetColor(propertyName, color);
    }

    void ApplyDefaultColorValue(Material material, string propertyName, object defaultColor)
    {
        if (defaultColor != null) { SetMaterialColor(material, propertyName, (Color)defaultColor); }
    }

    void SetMaterialFloatEnumValue(Material material, string propertyName, string enumValue, string[] acceptableValues)
    {
        if (enumValue == null) { return; }
        if (acceptableValues == null) { return; }

        int nAcceptableValues = acceptableValues.Length;
        for (int v = 0; v < nAcceptableValues; v++)
        {
            if (enumValue == acceptableValues[v])
            {
                SetMaterialFloat(material, propertyName, (float)v);
            }
        }
    }


    float ClampToFloatRange(float value, object[] range)
    {
        if (range == null) { return value; }

        float min = (float)range[0];
        if (value < min) { return min; }

        if (range.Length > 1)
        {
            float max = (float)range[1];
            if (value > max) { return max; }
        }

        return value;
    }

    int ClampToIntRange(int value, object[] range)
    {
        if (range == null) { return value; }

        int min = (int)range[0];
        if (value < min) { return min; }

        if (range.Length > 1)
        {
            int max = (int)range[1];
            if (value > max) { return max; }
        }

        return value;
    }

    bool TryConvertDataListToColor(DataList list, out Color color)
    {
        color = new Color();

        int nColorAttributes = 3;
        if (list.Count < nColorAttributes) { return false; }

        bool allAttributesOk = true;
        for (int i = 0; i < nColorAttributes; i++)
        {
            allAttributesOk &= (list[i].TokenType == TokenType.Double);
        }
        if (!allAttributesOk) { return false; }

        color.r = Mathf.Clamp((float)(double)list[0], 0, 1);
        color.g = Mathf.Clamp((float)(double)list[1], 0, 1);
        color.b = Mathf.Clamp((float)(double)list[2], 0, 1);
        color.a = 1.0f;

        return true;
    }

    void SetMaterialGltfTexture(Material mat, string propertyName, DataDictionary gltfMaterialTexture, GLBLoader loader)
    {
        bool hasIndex = gltfMaterialTexture.TryGetValue("index", TokenType.Double, out DataToken indexToken);
        if (!hasIndex) { return; }
        int index = (int)(double)indexToken;

        if (index < 0) { return; }

        Texture[] loaderTextures = loader.m_textures;
        if (index >= loaderTextures.Length) {return; }

        Texture texture = loaderTextures[index];
        string textureName = texture != null ? texture.name : "Null texture";
        mat.SetTexture(propertyName, texture);

        HandleKhrTextureTransform(gltfMaterialTexture, out Vector2 textureOffset, out Vector2 textureScale);
        mat.SetTextureOffset(propertyName, textureOffset);
        mat.SetTextureScale(propertyName, textureScale);
    }

    bool IsListComponentType(DataList list, TokenType type)
    {
        bool allOk = true;
        int nElements = list.Count; ;
        for (int e = 0; e < nElements; e++)
        {
            allOk |= (list[e].TokenType == type);
        }
        return allOk;
    }

    Vector2 DictOptVector2(DataDictionary dict, string fieldName, Vector2 defaultValue)
    {
        Vector2 retValue = defaultValue;
        if (dict.TryGetValue(fieldName, TokenType.DataList, out DataToken dataListToken))
        {
            DataList list = (DataList)dataListToken;
            if ((list.Count >= 2) && (IsListComponentType(list, TokenType.Double)))
            {
                retValue = DataListToVector2(list);
            }
        }
        return retValue;
    }

    Vector2 DataListToVector2(DataList list)
    {
        return new Vector2((float)(double)list[0], (float)(double)list[1]);
    }

    void HandleKhrTextureTransform(DataDictionary textureInfo, out Vector2 outOffset, out Vector2 outScale)
    {
        outOffset = defaultOffset;
        outScale = defaultScale;
        bool gotExtensions = textureInfo.TryGetValue(
            "extensions",
            TokenType.DataDictionary,
            out DataToken extensionsDictToken);
        if (!gotExtensions) { return; }

        DataDictionary extensions = (DataDictionary)extensionsDictToken;
        bool gotExtension = extensions.TryGetValue("KHR_texture_transform", TokenType.DataDictionary, out DataToken extensionToken);
        if (!gotExtension) { return; }

        DataDictionary textureTransform = (DataDictionary)extensionToken;

        outOffset = DictOptVector2(textureTransform, "offset", Vector2.zero);
        outScale = DictOptVector2(textureTransform, "scale", Vector2.one);
    }

    void TryApplyProperty(GLBLoader loader, Material mat, DataDictionary gltfMtoon, object[] propertyInfo)
    {
        string gltfPropertyName = (string)propertyInfo[gltfPropertyNameIndex];
        string shaderPropertyName = (string)propertyInfo[unityPropertyIndex];
        int expectedType = (int)propertyInfo[shaderPropertyTypeIndex];
        object defaultValue = null;
        if (DoesPropertyContainsDefault(propertyInfo)) { defaultValue = propertyInfo[defaultValueIndex]; }
        object[] valueRange = null;
        if (DoesPropertyContainsValuesRange(propertyInfo)) { valueRange = (object[])propertyInfo[acceptableValuesRangeIndex]; }

        switch(expectedType)
        {
            case boolType:
                {
                    if (defaultValue != null) { SetMaterialFloat(mat, shaderPropertyName, (bool)defaultValue ? 1 : 0); }
                    bool gotValue = gltfMtoon.TryGetValue(gltfPropertyName, TokenType.Boolean, out DataToken boolToken);
                    if (!gotValue) { return; }
                    bool value = (bool)boolToken;
                    SetMaterialFloat(mat, shaderPropertyName, value ? 1 : 0);
                }
                break;
            case intType:
                {
                    ApplyDefaultIntValue(mat, shaderPropertyName, defaultValue);

                    bool gotValue = gltfMtoon.TryGetValue(gltfPropertyName, TokenType.Double, out DataToken doubleToken);
                    if (!gotValue) { return; }
                    int value = (int)(double)doubleToken;

                    value = ClampToIntRange(value, valueRange);
                    SetMaterialInt(mat, shaderPropertyName, value);
                }
                break;
            case floatType:
                {
                    ApplyDefaultFloatValue(mat, shaderPropertyName, defaultValue);

                    bool gotValue = gltfMtoon.TryGetValue(gltfPropertyName, TokenType.Double, out DataToken doubleToken);
                    if (!gotValue) { return; }
                    float value = (float)(double)doubleToken;

                    value = ClampToFloatRange(value, valueRange);
                    SetMaterialFloat(mat, shaderPropertyName, value);
                }
                break;
            case rgbType:
                {
                    ApplyDefaultColorValue(mat, shaderPropertyName, defaultValue);

                    bool gotValue = gltfMtoon.TryGetValue(gltfPropertyName, TokenType.DataList, out DataToken listToken);
                    if (!gotValue) { return; }
                    bool convertedSuccessfully = TryConvertDataListToColor((DataList)listToken, out Color color);
                    if (!convertedSuccessfully) { return; }
                    SetMaterialColor(mat, shaderPropertyName, color);
                    

                }
                break;
            case gltfTextureType:
                {
                    bool gotValue = gltfMtoon.TryGetValue(gltfPropertyName, TokenType.DataDictionary, out DataToken dictionaryToken);
                    
                    if (!gotValue) { return; }
                    string[] keywords = (string[])propertyInfo[unityShaderKeywordsIndex];
                    if (keywords != null)
                    {
                        int nKeywords = keywords.Length;
                        for (int k = 0; k < nKeywords; k++)
                        {
                            string keyword = keywords[k];
                            mat.EnableKeyword(keyword);
                        }
                    }
                    SetMaterialGltfTexture(mat, shaderPropertyName, (DataDictionary)dictionaryToken, loader);
                }
                break;
            case floatEnumType:
                {
                    SetMaterialFloatEnumValue(mat, shaderPropertyName, (string)defaultValue, (string[])valueRange);
                    bool gotValue = gltfMtoon.TryGetValue(gltfPropertyName, TokenType.String, out DataToken stringToken);
                    if (!gotValue) { return; }
                    SetMaterialFloatEnumValue(mat, shaderPropertyName, (string)stringToken, (string[])valueRange);
                }
                break;
        }
        

    }

    const string unitySrcBlend = "_M_SrcBlend";
    const string unityDstBlend = "_M_DstBlend";
    const string unityAlphaToMask = "_M_AlphaToMask";
    const string unityZWrite = "_M_ZWrite";
    const string unityCullMode = "_M_CullMode";

    void SetupRenderingMode(Material material, DataDictionary mainDefinition, DataDictionary extensionDefinition)
    {
        int alphaMode = 0;
        if (mainDefinition.TryGetValue("alphaMode", TokenType.String, out DataToken alphaModeStringToken))
        {
            string mode = (string)alphaModeStringToken;
            if (mode == "MASK") { alphaMode = 1; }
            if (mode == "BLEND") { alphaMode = 2; }
        }

        int zWrite = 0;
        if (extensionDefinition.TryGetValue("transparentWithZWrite", TokenType.Double, out DataToken transparentWithZWriteToken))
        {
            zWrite = (int)(double)transparentWithZWriteToken;
        }

        int renderQueueOffset = 0;
        if (extensionDefinition.TryGetValue("renderQueueOffsetNumber", TokenType.Double, out DataToken renderQueueNumberToken))
        {
            renderQueueOffset = (int)(double)renderQueueNumberToken;
        }

        switch (alphaMode)
        {
            case 0:
                material.SetOverrideTag("RenderType", "Opaque");
                material.SetInt(unitySrcBlend, (int)BlendMode.One);
                material.SetInt(unityDstBlend, (int)BlendMode.Zero);
                material.SetInt(unityZWrite, 1);
                material.SetInt(unityAlphaToMask, 0);

                material.renderQueue = (int)RenderQueue.Geometry;
                break;
            case 1:
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetInt(unitySrcBlend, (int)BlendMode.One);
                material.SetInt(unityDstBlend, (int)BlendMode.Zero);
                material.SetInt(unityZWrite, 1);
                material.SetInt(unityAlphaToMask, 1);

                material.renderQueue = (int)RenderQueue.AlphaTest;
                break;
            case 2:
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt(unitySrcBlend, (int)BlendMode.SrcAlpha);
                material.SetInt(unityDstBlend, (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt(unityZWrite, zWrite);
                material.SetInt(unityAlphaToMask, 0);

                if (zWrite == 0)
                {
                    
                    renderQueueOffset = Mathf.Clamp(renderQueueOffset, -9, 0);
                    material.renderQueue = (int)RenderQueue.Transparent + renderQueueOffset;
                    
                }
                else
                {
                    renderQueueOffset = Mathf.Clamp(renderQueueOffset, 0, +9);
                    material.renderQueue = (int)RenderQueue.GeometryLast + 1 + renderQueueOffset; // Transparent First + N
                }

                break;
        }

    }

    public override void HandleInternal(Material material, DataDictionary extensionDefinition, DataDictionary mainMaterialDefinition, GLBLoader loader)
    {
        if (material.shader != templateMaterial.shader)
        {
            material.shader = templateMaterial.shader;
        }
        
        int nProperties = mToonProperties.Length;
        for (int i = 0; i < nProperties; i++)
        {
            TryApplyProperty(loader, material, extensionDefinition, (object[])mToonProperties[i]);
        }

        if (mainMaterialDefinition.ContainsKey("emissiveTexture"))
        {
            material.EnableKeyword("_MTOON_EMISSIVEMAP");
        }

        if (mainMaterialDefinition.ContainsKey("normalTexture"))
        {
            material.EnableKeyword("_NORMALMAP");
        }

        if (extensionDefinition.TryGetValue("transparentWithZWrite", TokenType.Double, out DataToken zWriteModeToken))
        {
            int zWrite = (int)(double)zWriteModeToken;
            material.SetInt("_M_ZWrite", zWrite);
        }
        if (mainMaterialDefinition.TryGetValue("doubleSided", TokenType.Boolean, out DataToken doubleSidedToken))
        {
            bool doubleSided = (bool)doubleSidedToken;
            material.SetInt("_DoubleSided", doubleSided ? 1 : 0);
        }


        SetupRenderingMode(material, mainMaterialDefinition, extensionDefinition);

        int currentOutlineMode = (int)material.GetFloat("_OutlineWidthMode");
        if (currentOutlineMode == 1)
        {
            material.EnableKeyword("_MTOON_OUTLINE_WORLD");
        }
        else if (currentOutlineMode == 2)
        {
            material.EnableKeyword("_MTOON_OUTLINE_SCREEN");
        }
    }

    public override string HandledExtensionName()
    {
        return "VRMC_materials_mtoon";
    }

}
