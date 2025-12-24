
using UdonSharp;
using UnityEngine;
using VoyageVoyage;
using VRC.SDK3.Data;

public class ShaderMotionMeshPlayerMaterialExtension : MaterialExtensionHandler
{
    public Material meshPlayerShader;

    string[] floatProperties = new string[]
    {
        "alphaTest", "_AlphaTest",
        "cull", "_Cull",
        "alphaCutOff", "_Cutoff",
        "humanScale", "_HumanScale",
        "layer", "_Layer",
        "rotationTolerance", "_RotationTolerance",
        "color", "_Color"
    };

    string[] textureProperties = new string[]
    {
        "boneTexture", "_Bone",
        "mainTexture", "_MainTex",
        "shapeTexture", "_Shape",
    };

    public override string HandledExtensionName()
    {
        return "EXT_voyage_shadermotion";
    }

    void SetMaterialGltfTexture(Material mat, string propertyName, DataDictionary gltfMaterialTexture, GLBLoader loader)
    {
        bool hasIndex = gltfMaterialTexture.TryGetValue("index", TokenType.Double, out DataToken indexToken);
        if (!hasIndex) { return; }
        int index = (int)(double)indexToken;

        if (index < 0) { return; }

        Texture[] loaderTextures = loader.m_textures;
        if (index >= loaderTextures.Length) { return; }

        Texture texture = loaderTextures[index];
        string textureName = texture != null ? texture.name : "Null texture";
        mat.SetTexture(propertyName, texture);
    }

    public override void HandleInternal(Material material, DataDictionary extensionDefinition, DataDictionary mainMaterialDefinition, GLBLoader loader)
    {
        if (material.shader != meshPlayerShader.shader)
        {
            int renderQueue = material.renderQueue;
            material.shader = meshPlayerShader.shader;
            material.renderQueue = renderQueue;
        }
        int nFloatProperties = floatProperties.Length;
        for (int i = 0; i < nFloatProperties; i += 2)
        {
            string gltfProperty = floatProperties[i];
            bool gotValue = extensionDefinition.TryGetValue(gltfProperty, TokenType.Double, out DataToken valueToken);
            if (!gotValue) { continue; }

            string floatProperty = floatProperties[i + 1];

            float value = (float)(double)valueToken;
            material.SetFloat(floatProperty, value);
        }

        int nTextureProperties = textureProperties.Length;
        for (int i = 0; i < nTextureProperties; i += 2)
        {
            string gltfProperty = textureProperties[i];
            bool gotValue = extensionDefinition.TryGetValue(gltfProperty, TokenType.DataDictionary, out DataToken valueToken);
            if (!gotValue) { continue; }

            string textureProperty = textureProperties[i + 1];

            DataDictionary textureDictionary = (DataDictionary)valueToken;
            SetMaterialGltfTexture(material, textureProperty, textureDictionary, loader);
        }

        Texture tex = meshPlayerShader.GetTexture("_MotionDec");
        if (tex != null) { material.SetTexture("_MotionDec", tex); }
    }

}
