using UnityEngine;
using UnityEngine.Rendering;

public static class TextureUtilities
{
    /// <summary>
    /// 从2D的Nosie贴图生成3D的Noise贴图
    /// </summary>
    /// <param name="tex">2D的Nosie贴图</param>
    /// <param name="dimensions">生成的3D的Noise贴图的尺寸</param>
    public static Texture3D CreateFogLUT3DFrom2DSlices(Texture2D tex, Vector3Int dimensions)
    {
        Texture2D readableTexture2D = GenerateReadableTexture(tex);

        Color[] colors = new Color[dimensions.x * dimensions.y * dimensions.z];

        int idx = 0;
        for (int z = 0; z < dimensions.z; ++z)
        {
            for (int y = 0; y < dimensions.y; ++y)
            {
                for (int x = 0; x < dimensions.x; ++x, ++idx)
                {
                    colors[idx] = readableTexture2D.GetPixel(x + z * dimensions.z, y);
                }
            }
        }

        Texture3D texture3D = new Texture3D(dimensions.x, dimensions.y, dimensions.z, TextureFormat.RGBAHalf, true);
        texture3D.SetPixels(colors);
        texture3D.Apply();

        return texture3D;
    }

    public static RenderTexture CreateFogLUT3DFrom2DSlicesCompute(Texture2D fogTexture, Vector3Int dimensions, ComputeShader shader)
    {
        RenderTexture fogLut3D = new RenderTexture(dimensions.x
            , dimensions.y
            , 0
            , RenderTextureFormat.ARGB32
            , RenderTextureReadWrite.Linear)
        {
            volumeDepth = dimensions.z,
            dimension = TextureDimension.Tex3D,
            enableRandomWrite = true,
            name = "FogLUT3DFrom2D"
        };

        fogLut3D.Create();

        int kernel = shader.FindKernel("Create3DLUTFrom2D");
        shader.SetTexture(kernel, "_FogLUT3DFrom2D", fogLut3D);
        shader.SetTexture(kernel, "_FogTexture2D", fogTexture);
        shader.Dispatch(kernel, dimensions.x, dimensions.y, dimensions.z);

        return fogLut3D;
    }

    public static RenderTexture CreateFogLUT3DFromSimplexNoise(Vector3Int dimensions, ComputeShader shader)
    {
        RenderTexture fogLut3D = new RenderTexture(dimensions.x
            , dimensions.y
            , 0
            , RenderTextureFormat.RGBAUShort
            , RenderTextureReadWrite.Linear)
        {
            volumeDepth = dimensions.z,
            dimension = TextureDimension.Tex3D,
            enableRandomWrite = true,
            name = "FogLUT3DSnoise"
        };
        fogLut3D.Create();

        int kernel = shader.FindKernel("Create3DLUTSimplexNoise");
        shader.SetTexture(kernel, "_FogLUT3DSNoise", fogLut3D);
        shader.Dispatch(kernel, dimensions.x, dimensions.y, dimensions.z);

        return fogLut3D;
    }

    /// <summary>
    /// 从texture生成一个新的可读的Texture2D<see cref="Texture2D.isReadable"/>>
    /// https://support.unity3d.com/hc/en-us/articles/206486626-How-can-I-get-pixels-from-unreadable-textures
    /// </summary>
    private static Texture2D GenerateReadableTexture(Texture2D texture)
    {
        // Create a temporary RenderTexture of the same size as the texture
        RenderTexture tmp = RenderTexture.GetTemporary(texture.width
            , texture.height
            , 0
            , RenderTextureFormat.Default
            , RenderTextureReadWrite.Linear);

        // Blit the pixels on texture to the RenderTexture
        Graphics.Blit(texture, tmp);
        RenderTexture previous = RenderTexture.active;
        // Set the current RenderTexture to the temporary one we created
        RenderTexture.active = tmp;
        // Create a new readable Texture2D to copy the pixels to it
        Texture2D myTexture2D = new Texture2D(texture.width, texture.height);
        // Copy the pixels from the RenderTexture to the new Texture
        myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
        myTexture2D.Apply();
        // Reset the active RenderTexture
        RenderTexture.active = previous;
        // Release the temporary RenderTexture
        RenderTexture.ReleaseTemporary(tmp);
        return myTexture2D;
    }
}