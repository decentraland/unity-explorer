using ECS.Unity.Materials.Components;
using System;

namespace ECS.Unity.Materials
{
    internal static class MaterialDataEqualityComparer
    {
        public static bool Equals(in MaterialData x, in MaterialData y) =>
            x.IsPbrMaterial == y.IsPbrMaterial
            && x.Textures.Equals(y.Textures)
            && x.AlphaTest.Equals(y.AlphaTest)
            && x.CastShadows == y.CastShadows
            && x.AlbedoColor.Equals(y.AlbedoColor)
            && x.DiffuseColor.Equals(y.DiffuseColor)
            && x.EmissiveColor.Equals(y.EmissiveColor)
            && x.ReflectivityColor.Equals(y.ReflectivityColor)
            && x.TransparencyMode == y.TransparencyMode
            && x.Metallic.Equals(y.Metallic)
            && x.Roughness.Equals(y.Roughness)
            && x.SpecularIntensity.Equals(y.SpecularIntensity)
            && x.EmissiveIntensity.Equals(y.EmissiveIntensity)
            && x.DirectIntensity.Equals(y.DirectIntensity);

        public static int GetHashCode(MaterialData obj)
        {
            var hashCode = new HashCode();
            hashCode.Add(obj.IsPbrMaterial);
            hashCode.Add(obj.Textures);
            hashCode.Add(obj.AlphaTest);
            hashCode.Add(obj.CastShadows);
            hashCode.Add(obj.AlbedoColor);
            hashCode.Add(obj.DiffuseColor);
            hashCode.Add(obj.EmissiveColor);
            hashCode.Add(obj.ReflectivityColor);
            hashCode.Add((int)obj.TransparencyMode);
            hashCode.Add(obj.Metallic);
            hashCode.Add(obj.Roughness);
            hashCode.Add(obj.SpecularIntensity);
            hashCode.Add(obj.EmissiveIntensity);
            hashCode.Add(obj.DirectIntensity);
            return hashCode.ToHashCode();
        }
    }
}
