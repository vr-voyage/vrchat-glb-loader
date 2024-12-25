using System;
using UnityEngine;
using UdonSharp;

public class DDSReader : UdonSharpBehaviour
{
    const int DDS_FILE_MAGIC = 0x20534444;
    const int DDS_FILE_MAGIC_SIZE = 4;
    const int DDS_HEADER_SIZE = 124;
    const int DDS_HEADER_WITH_DXT10_SIZE = 144;
    const int DDSD_CAPS = 0x1;
    const int DDSD_HEIGHT = 0x2;
    const int DDSD_WIDTH = 0x4;
    const int DDSD_PIXELFORMAT = 0x1000;

    const int REQUIRED_DDSD_FLAGS = DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT;

    const int FOURCC_DXT1 = 0x31545844;
    const int FOURCC_DX10 = 0x30315844;
    const int FOURCC_BC5 = 0x32495441;
    const int FOURCC_DXT5 = 0x35545844;

    const int DXGI_FORMAT_BC7_UNORM = 98;
    const int DXGI_FORMAT_BC7_UNORM_SRGB = 99;

    bool IsDx10ExtensionAboutBc7(byte[] data, int dds10HeaderStart)
    {
        uint dxgiFormat = BitConverter.ToUInt32(data, dds10HeaderStart);
        return dxgiFormat == DXGI_FORMAT_BC7_UNORM || dxgiFormat == DXGI_FORMAT_BC7_UNORM_SRGB;
    }

    void ReportError(string tag, string errorMessage)
    {
        Debug.LogError($"[{tag}] {errorMessage}");
    }

    public Texture2D Parse(byte[] ddsData, int startFrom) 
    {
        if (ddsData.Length < DDS_HEADER_SIZE + DDS_FILE_MAGIC_SIZE)
        {
            ReportError("ReadDDSTexture", $"Not enough data : {ddsData.Length} < {DDS_HEADER_SIZE}");
            return null;
        }

        int cursor = startFrom;
        if (BitConverter.ToUInt32(ddsData, cursor) != DDS_FILE_MAGIC)
        {
            ReportError("Start", "Not a DDS FILE !");
            return null;
        }
        cursor += 4;

        int structureSize = System.BitConverter.ToInt32(ddsData, cursor);
        cursor += 4;

        if (structureSize != DDS_HEADER_SIZE)
        {
            ReportError("ReadDDSTexture", $"Wrong header. Expected value {DDS_HEADER_SIZE}, got {structureSize}");
            return null;
        }

        int dwFlags = System.BitConverter.ToInt32(ddsData, cursor);
        cursor += 4;

        if ((dwFlags & REQUIRED_DDSD_FLAGS) != REQUIRED_DDSD_FLAGS)
        {
            ReportError("ReadDDSTexture", $"The required flags {REQUIRED_DDSD_FLAGS:X} are not set ({dwFlags:X}) !");
            return null;
        }
  
        int imageHeight = System.BitConverter.ToInt32(ddsData, cursor);
        cursor += 4;
        
        int imageWidth = System.BitConverter.ToInt32(ddsData, cursor);
        cursor += 4;
     
        int pitch = System.BitConverter.ToInt32(ddsData, cursor);
        cursor += 4;
      
        int depth = System.BitConverter.ToInt32(ddsData, cursor);
        cursor += 4;
    
        int mipmapCount = System.BitConverter.ToInt32(ddsData, cursor);
        cursor += 4;

        /* Skip 11 reserved DWORD */

        cursor += (4 * 11);

        int pixelFormatSize = System.BitConverter.ToInt32(ddsData, cursor);
        cursor += 4;

        int pixelFormatFlags = System.BitConverter.ToInt32(ddsData, cursor);
        cursor += 4;

        int dx9FourCC = System.BitConverter.ToInt32(ddsData, cursor);

        TextureFormat format = TextureFormat.RGBA32;

        int dataStart = startFrom + DDS_FILE_MAGIC_SIZE + DDS_HEADER_SIZE;

        switch (dx9FourCC)
        {
            case FOURCC_DX10:
                int extensionHeaderPosition = dataStart;
                bool isBc7 = IsDx10ExtensionAboutBc7(ddsData, extensionHeaderPosition);

                if (isBc7)
                {
                    dataStart = startFrom + DDS_FILE_MAGIC_SIZE + DDS_HEADER_WITH_DXT10_SIZE;
                    format = TextureFormat.BC7;
                }
                else
                {
                    ReportError("ReadDDSTexture", "Not a BC7 texture");
                    return null;
                }
                break;
            case FOURCC_DXT1:
                format = TextureFormat.DXT1;
                Debug.Log("DXT1");
                break;
            case FOURCC_BC5:
                format = TextureFormat.BC5;
                Debug.Log("BC5");
                break;
            case FOURCC_DXT5:
                format = TextureFormat.DXT5;
                Debug.Log("DXT5");
                break;
            default:
                ReportError("ReadDDSTexture", "Unknown DDS texture format");
                return null;

        }

        bool hasMipmap = mipmapCount > 1;

        byte[] content = new byte[ddsData.Length - dataStart];
        Buffer.BlockCopy(ddsData, dataStart, content, 0, content.Length);

        Texture2D retTexture = new Texture2D(imageWidth, imageHeight, format, hasMipmap);
        retTexture.LoadRawTextureData(content);
        retTexture.Apply(true, false);

        return retTexture;
    }

}
