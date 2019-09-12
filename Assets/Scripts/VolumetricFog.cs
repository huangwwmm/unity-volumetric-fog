using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
class VolumetricFog : MonoBehaviour
{
    private const RenderTextureFormat FORMAT_FOGRENDER_TEXTURE = RenderTextureFormat.ARGBHalf;

    private const string SK_SHADOWS_ON = "SHADOWS_ON";
    private const string SK_SHADOWS_OFF = "SHADOWS_OFF";

    private static readonly int SP_BLUE_NOISE_TEXTURE = Shader.PropertyToID("_BlueNoiseTexture");
    private static readonly int SP_FOG_SPEED = Shader.PropertyToID("_FogSpeed");
    private static readonly int SP_FOG_DIRECTION = Shader.PropertyToID("_FogDirection");
    private static readonly int SP_AMBIENT_FOG = Shader.PropertyToID("_AmbientFog");
    private static readonly int SP_LIGHT_DIR = Shader.PropertyToID("_LightDir");
    private static readonly int SP_FOG_COLOR = Shader.PropertyToID("_FogColor");
    private static readonly int SP_SHADOW_COLOR = Shader.PropertyToID("_ShadowColor");
    private static readonly int SP_LIGHT_COLOR = Shader.PropertyToID("LightColor");
    private static readonly int SP_LIGHT_INTENSITY = Shader.PropertyToID("_LightIntensity");
    private static readonly int SP_FOG_SIZE = Shader.PropertyToID("_FogSize");
    private static readonly int SP_FOG_WORLD_POSITION = Shader.PropertyToID("_FogWorldPosition");
    private static readonly int SP_HEIGHT_DENSITY_COEF = Shader.PropertyToID("_HeightDensityCoef");
    private static readonly int SP_BASE_HEIGHT_DENSITY = Shader.PropertyToID("_BaseHeightDensity");
    private static readonly int SP_ANISOTROPY = Shader.PropertyToID("_Anisotropy");
    private static readonly int SP_EXTINCTION_COEF = Shader.PropertyToID("_ExtinctionCoef");
    private static readonly int SP_NOISE_SCALE = Shader.PropertyToID("_NoiseScale");
    private static readonly int SP_FOG_DENSITY = Shader.PropertyToID("_FogDensity");
    private static readonly int SP_RAYMARCH_STEPS = Shader.PropertyToID("_RaymarchSteps");
    private static readonly int SP_RAYLEIGH_SCATTERING_COEF = Shader.PropertyToID("_RayleighScatteringCoef");
    private static readonly int SP_BLUR_DEPTH_FALLOFF = Shader.PropertyToID("_BlurDepthFalloff");
    private static readonly int SP_OFFSETS = Shader.PropertyToID("_BlurOffsets");
    private static readonly int SP_BLUR_WEIGHTS = Shader.PropertyToID("_BlurWeights");
    private static readonly int SP_BLUR_DIR = Shader.PropertyToID("BlurDir");
    private static readonly int SP_FOG_RENDERTARGET_LINEAR = Shader.PropertyToID("FogRendertargetLinear");
    private static readonly int SP_MIE_SCATTERING_COEF = Shader.PropertyToID("_MieScatteringCoef");
    private static readonly int SP_KFACTOR = Shader.PropertyToID("_kFactor");
    private static readonly int SP_NOISE_TEX3D = Shader.PropertyToID("_NoiseTex3D");
    private static readonly int SP_INVERSE_PROJECTION_MATRIX = Shader.PropertyToID("InverseProjectionMatrix");
    private static readonly int SP_INVERSE_VIEW_MATRIX = Shader.PropertyToID("InverseViewMatrix");

    [Header("Required assets")]
    /// <summary>
    /// 用于计算每个体素的雾密度和颜色着色器
    /// </summary>
    public Shader CalculateFogShader;
    /// <summary>
    /// 将模糊应用于Calculate Fog Shader生成的纹理的着色器
    /// </summary>
    public Shader ApplyBlurShader;
    /// <summary>
    /// 将雾和几何体混合在一起的着色器
    /// </summary>
    public Shader ApplyFogShader;
    public ComputeShader Create3DLUTShader;
    /// <summary>
    /// 场景中的定向光
    /// </summary>
    public Light SunLight;
    public List<Light> FogLightCasters;
    /// <summary>
    /// 噪声的纹理，用于创建3D雾纹理
    /// </summary>
    public Texture2D FogTexture2D;
    public Texture2D BlueNoiseTexture2D;

    [Header("Position and size(in m³)")]
    /// <summary>
    /// 是否限制体积雾大小
    /// </summary>
    public bool LimitFogInSize = true;
    /// <summary>
    /// 体积雾的中心点坐标
    /// </summary>
    public Vector3 FogWorldPosition;
    /// <summary>
    /// 体积雾大小
    /// </summary>
    public float FogSize = 10.0f;

    [Header("Performance")]
    /// <summary>
    /// 	用于渲染体积雾效果的最大步数
    /// </summary>
    [Range(16, 256)]
    public int RayMarchSteps = 128;

    [Header("Physical coefficients")]
    /// <summary>
    /// 是否使用Rayleigh散射
    /// </summary>
    public bool UseRayleighScattering = true;
    /// <summary>
    /// Rayleigh散射的系数
    /// </summary>
    public float RayleighScatteringCoef = 0.25f;
    /// <summary>
    /// Mie散射的系数
    /// </summary>
    public float MieScatteringCoef = 0.25f;
    /// <summary>
    /// 确定用于近似米氏散射的函数
    /// </summary>
    public MieScatteringApproximation MieScatteringApproximation = MieScatteringApproximation.HenyeyGreenstein;
    /// <summary>
    /// 在每个体素中添加到雾值的系数
    /// </summary>
    public float FogDensityCoef = 0.3f;
    /// <summary>
    /// 在每个体素中，计算消光的系数
    /// </summary>
    public float ExtinctionCoef = 0.01f;
    /// <summary>
    /// 光主要散射的方向。 值-1表示所有光线向后散射回光源，值1表示所有光线都向观察者散射
    /// </summary>
    [Range(-1, 1)]
    public float Anisotropy = 0.5f;
    /// <summary>
    /// 如果启用高度雾，确定雾密度衰减的速率
    /// </summary>
    public float HeightDensityCoef = 0.5f;
    /// <summary>
    /// 底部的体积雾密度
    /// </summary>
    public float BaseHeightDensity = 0.5f;

    [Header("Blur")]
    /// <summary>
    /// 模糊次数
    /// </summary>
    [Range(1, 8)]
    public int BlurIterations = 4;
    /// <summary>
    /// 不添加模糊的范围
    /// </summary>
    public float BlurDepthFalloff = 0.5f;
    /// <summary>
    /// 模糊时，每个相邻像素的偏移值
    /// </summary>
    public Vector3 BlurOffsets = new Vector3(1, 2, 3);
    /// <summary>
    /// 模糊时，每个相邻像素的权重
    /// </summary>
    public Vector3 BlurWeights = new Vector3(0.213f, 0.17f, 0.036f);

    [Header("Color")]
    public bool UseLightColorForFog = false;
    /// <summary>
    /// 在阴影中体积雾的颜色
    /// </summary>
    public Color FogInShadowColor = Color.black;
    /// <summary>
    /// 在光照中体积雾的颜色
    /// </summary>
    public Color FogInLightColor = Color.white;
    /// <summary>
    /// 阴影区域的雾量
    /// </summary>
    [Range(0, 1)]
    public float AmbientFog;

    [Header("Animation")]
    public Vector3 WindDirection = Vector3.right;
    public float Speed = 1f;

    /// <summary>
    /// 是否启用雾和背景的混合
    /// </summary>
    public bool AddSceneColor;
    /// <summary>
    /// 是否启用模糊
    /// </summary>
    public bool BlurEnabled;
    /// <summary>
    /// 是否启用阴影
    /// </summary>
    public bool ShadowsEnabled;
    /// <summary>
    /// 是否启用高度雾
    /// </summary>
    public bool HeightFogEnabled;

    [Range(-100, 100)]
    public float NoiseScale = 0f;
    public Vector3Int Noise3DTextureDimensions = Vector3Int.one;

    private Material m_ApplyBlurMaterial;
    private Material m_CalculateFogMaterial;
    private Material m_ApplyFogMaterial;

    private float m_KFactor;

    private Texture3D m_FogTexture3D;

    private CommandBuffer m_AfterShadowPass;

    private Camera m_CurrentCamera;

    protected void OnEnable()
    {
        m_CalculateFogMaterial = new Material(CalculateFogShader);
        m_CalculateFogMaterial.hideFlags = HideFlags.HideAndDontSave;
        m_ApplyBlurMaterial = new Material(ApplyBlurShader);
        m_ApplyBlurMaterial.hideFlags = HideFlags.HideAndDontSave;
        m_ApplyFogMaterial = new Material(ApplyFogShader);
        m_ApplyFogMaterial.hideFlags = HideFlags.HideAndDontSave;

        m_CurrentCamera = GetComponent<Camera>();

        #region Add Light CommandBuffer
        // based on https://interplayoflight.wordpress.com/2015/07/03/adventures-in-postprocessing-with-unity/    
        m_AfterShadowPass = new CommandBuffer { name = "Volumetric Fog ShadowMap" };
        m_AfterShadowPass.SetGlobalTexture("ShadowMap"
            , new RenderTargetIdentifier(BuiltinRenderTextureType.CurrentActive));

        for (int iLight = 0; iLight < FogLightCasters.Count; iLight++)
        {
            Light iterLight = FogLightCasters[iLight];
            iterLight.AddCommandBuffer(LightEvent.AfterShadowMap, m_AfterShadowPass);
        }
        #endregion

        // Generate 3DTexture
        m_FogTexture3D = TextureUtilities.CreateFogLUT3DFrom2DSlices(FogTexture2D, Noise3DTextureDimensions);
    }

    protected void OnDisable()
    {
        #region Remove Light CommandBuffer
        // based on https://interplayoflight.wordpress.com/2015/07/03/adventures-in-postprocessing-with-unity/    
        for (int iLight = 0; iLight < FogLightCasters.Count; iLight++)
        {
            Light iterLight = FogLightCasters[iLight];
            iterLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, m_AfterShadowPass);
        }
        #endregion
    }

    [ImageEffectOpaque]
    protected void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (ShadowsEnabled)
        {
            Shader.EnableKeyword(SK_SHADOWS_ON);
            Shader.DisableKeyword(SK_SHADOWS_OFF);
        }
        else
        {
            Shader.DisableKeyword(SK_SHADOWS_ON);
            Shader.EnableKeyword(SK_SHADOWS_OFF);
        }

        SetMieScattering();

        m_CalculateFogMaterial.SetTexture(SP_NOISE_TEX3D, m_FogTexture3D);
        Shader.SetGlobalMatrix(SP_INVERSE_VIEW_MATRIX, m_CurrentCamera.cameraToWorldMatrix);
        Shader.SetGlobalMatrix(SP_INVERSE_PROJECTION_MATRIX, m_CurrentCamera.projectionMatrix.inverse);

        // 可以降低雾RT的大小
        RenderTexture fogRT1 = RenderTexture.GetTemporary(source.width, source.height, 0, FORMAT_FOGRENDER_TEXTURE);
        fogRT1.filterMode = FilterMode.Bilinear;
        RenderFog(fogRT1, source);

        RenderTexture fogRT2 = RenderTexture.GetTemporary(source.width, source.height, 0, FORMAT_FOGRENDER_TEXTURE);
        fogRT2.filterMode = FilterMode.Bilinear;
        BlurFog(fogRT1, fogRT2);
        RenderTexture.ReleaseTemporary(fogRT2);

        BlendWithScene(source, destination, fogRT1);
        RenderTexture.ReleaseTemporary(fogRT1);
    }

    private void CalculateKFactor()
    {
        m_KFactor = 1.55f * Anisotropy - (0.55f * Mathf.Pow(Anisotropy, 3));
    }

    private void RenderFog(RenderTexture fogRenderTexture, RenderTexture source)
    {
        if (UseRayleighScattering)
        {
            m_CalculateFogMaterial.EnableKeyword("RAYLEIGH_SCATTERING");
            m_CalculateFogMaterial.SetFloat(SP_RAYLEIGH_SCATTERING_COEF, RayleighScatteringCoef);
        }
        else
        {
            m_CalculateFogMaterial.DisableKeyword("RAYLEIGH_SCATTERING");
        }

        ToggleMaterialKeyword(m_CalculateFogMaterial, "LIMITFOGSIZE", LimitFogInSize);
        ToggleMaterialKeyword(m_CalculateFogMaterial, "HEIGHTFOG", HeightFogEnabled);

        m_CalculateFogMaterial.SetFloat(SP_RAYMARCH_STEPS, RayMarchSteps);

        m_CalculateFogMaterial.SetFloat(SP_FOG_DENSITY, FogDensityCoef);
        m_CalculateFogMaterial.SetFloat(SP_NOISE_SCALE, NoiseScale);


        m_CalculateFogMaterial.SetFloat(SP_EXTINCTION_COEF, ExtinctionCoef);
        m_CalculateFogMaterial.SetFloat(SP_ANISOTROPY, Anisotropy);
        m_CalculateFogMaterial.SetFloat(SP_BASE_HEIGHT_DENSITY, BaseHeightDensity);
        m_CalculateFogMaterial.SetFloat(SP_HEIGHT_DENSITY_COEF, HeightDensityCoef);

        m_CalculateFogMaterial.SetVector(SP_FOG_WORLD_POSITION, FogWorldPosition);
        m_CalculateFogMaterial.SetFloat(SP_FOG_SIZE, FogSize);
        m_CalculateFogMaterial.SetFloat(SP_LIGHT_INTENSITY, SunLight.intensity);

        m_CalculateFogMaterial.SetColor(SP_LIGHT_COLOR, SunLight.color);
        m_CalculateFogMaterial.SetColor(SP_SHADOW_COLOR, FogInShadowColor);
        m_CalculateFogMaterial.SetColor(SP_FOG_COLOR, UseLightColorForFog ? SunLight.color : FogInLightColor);

        m_CalculateFogMaterial.SetVector(SP_LIGHT_DIR, SunLight.transform.forward);
        m_CalculateFogMaterial.SetFloat(SP_AMBIENT_FOG, AmbientFog);

        m_CalculateFogMaterial.SetVector(SP_FOG_DIRECTION, WindDirection);
        m_CalculateFogMaterial.SetFloat(SP_FOG_SPEED, Speed);

        m_CalculateFogMaterial.SetTexture(SP_BLUE_NOISE_TEXTURE, BlueNoiseTexture2D);

        Graphics.Blit(source, fogRenderTexture, m_CalculateFogMaterial);
    }

    private void BlurFog(RenderTexture fogTarget1, RenderTexture fogTarget2)
    {
        if (!BlurEnabled) return;


        m_ApplyBlurMaterial.SetFloat(SP_BLUR_DEPTH_FALLOFF, BlurDepthFalloff);

        var blurOffsets = new Vector4(0, // initial sample is always at the center 
            BlurOffsets.x,
            BlurOffsets.y,
            BlurOffsets.z);

        m_ApplyBlurMaterial.SetVector(SP_OFFSETS, blurOffsets);

        // x is sum of all weights
        var blurWeightsWithTotal = new Vector4(BlurWeights.x + BlurWeights.y + BlurWeights.z,
            BlurWeights.x,
            BlurWeights.y,
            BlurWeights.z);

        m_ApplyBlurMaterial.SetVector(SP_BLUR_WEIGHTS, blurWeightsWithTotal);

        for (var i = 0; i < BlurIterations; i++)
        {
            // vertical blur 
            m_ApplyBlurMaterial.SetVector(SP_BLUR_DIR, new Vector2(0, 1));
            Graphics.Blit(fogTarget1, fogTarget2, m_ApplyBlurMaterial);

            // horizontal blur
            m_ApplyBlurMaterial.SetVector(SP_BLUR_DIR, new Vector2(1, 0));
            Graphics.Blit(fogTarget2, fogTarget1, m_ApplyBlurMaterial);
        }
    }

    private void BlendWithScene(RenderTexture source, RenderTexture destination, RenderTexture fogTarget)
    {
        if (!AddSceneColor)
        {
            Graphics.Blit(fogTarget, destination);
            return;
        };

        //send fog texture
        m_ApplyFogMaterial.SetTexture(SP_FOG_RENDERTARGET_LINEAR, fogTarget);

        //apply to main rendertarget
        Graphics.Blit(source, destination, m_ApplyFogMaterial);
    }

    /// <summary>
    /// 设置求米氏散射近似值的相函数
    /// </summary>
    private void SetMieScattering()
    {
        ToggleMaterialKeyword(m_CalculateFogMaterial, "HG_SCATTERING", false);
        ToggleMaterialKeyword(m_CalculateFogMaterial, "CS_SCATTERING", false);
        ToggleMaterialKeyword(m_CalculateFogMaterial, "SCHLICK_HG_SCATTERING", false);

        switch (MieScatteringApproximation)
        {
            case MieScatteringApproximation.HenyeyGreenstein:
                ToggleMaterialKeyword(m_CalculateFogMaterial, "HG_SCATTERING", true);
                m_CalculateFogMaterial.SetFloat(SP_MIE_SCATTERING_COEF, MieScatteringCoef);
                break;

            case MieScatteringApproximation.CornetteShanks:
                ToggleMaterialKeyword(m_CalculateFogMaterial, "CS_SCATTERING", true);
                m_CalculateFogMaterial.SetFloat(SP_MIE_SCATTERING_COEF, MieScatteringCoef);
                break;

            case MieScatteringApproximation.Schlick:
                CalculateKFactor();
                ToggleMaterialKeyword(m_CalculateFogMaterial, "SCHLICK_HG_SCATTERING", true);
                m_CalculateFogMaterial.SetFloat(SP_KFACTOR, m_KFactor);
                m_CalculateFogMaterial.SetFloat(SP_MIE_SCATTERING_COEF, MieScatteringCoef);
                break;
            case MieScatteringApproximation.Off:
                break;
            default:
                Debug.LogWarning($"Mie scattering approximation {MieScatteringApproximation} is not handled by SetMieScattering()");
                break;
        }
    }

    private void ToggleMaterialKeyword(Material shaderMat, string keyword, bool enabled)
    {
        if (enabled)
        {
            shaderMat.EnableKeyword(keyword);
        }
        else
        {
            shaderMat.DisableKeyword(keyword);
        }
    }
}