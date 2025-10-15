namespace ECS.Unity.Materials.Components
{
    /// <summary>
    ///     Flag component to indicate that the material requires video texture scaling.
    ///     This component is added when a material uses video textures and acts as a flag
    ///     for the ApplyVideoMaterialTextureScaleSystem to process the material.
    /// </summary>
    public struct MaterialScaleRequestComponent
    {
        public bool IsAlbedoVideoTexture;
        public bool IsAlphaVideoTexture;
    }
}
