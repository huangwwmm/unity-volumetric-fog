using System;

[Flags]
public enum NoiseSource
{
    Texture2D = 1,
    /// <summary>
    /// <see cref="Texture3D"/>
    /// </summary>
    Texture3D = 2,
    Texture3DCompute = 4,
    SimplexNoise = 8,
    SimplexNoiseCompute = 16,
}