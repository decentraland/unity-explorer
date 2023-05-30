namespace ECS.Unity.Materials.Components
{
    public enum MaterialTransparencyMode : byte
    {
        Opaque = 0,
        AlphaTest = 1,
        AlphaBlend = 2,
        AlphaTestAndAlphaBlend = 3,
        Auto = 4,
    }
}
