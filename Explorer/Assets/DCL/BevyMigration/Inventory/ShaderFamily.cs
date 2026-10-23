using System;

namespace DCL.BevyMigration.Inventory
{
    /// <summary>
    ///     The material families of the render bridge (BevyMigration/docs/README.md § 8). A shader is assigned to a
    ///     family by name; the inventory reports the assignment so that a new shader shows up as Unassigned
    ///     instead of silently joining a family.
    /// </summary>
    public enum ShaderFamily
    {
        Unassigned,
        UrpLit,
        Toon,
        Unlit,
        TmpSdf,
        Video,
        Ui,
        Landscape,
        Particles,
        Skybox,
        Internal,
    }

    public static class ShaderFamilies
    {
        /// <summary>
        ///     The step that ports the family, as the plan schedules it; null when the family never reaches Bevy.
        /// </summary>
        public static int? PortingStep(ShaderFamily family) =>
            family switch
            {
                ShaderFamily.UrpLit => 3,
                ShaderFamily.Unlit => 3,
                ShaderFamily.Toon => 4,
                ShaderFamily.TmpSdf => 4,
                ShaderFamily.Video => 4,
                ShaderFamily.Ui => 8,
                ShaderFamily.Landscape => 11,
                ShaderFamily.Particles => 11,
                ShaderFamily.Skybox => 11,
                _ => null,
            };

        public static ShaderFamily Classify(string shaderName)
        {
            if (Contains(shaderName, "TextMeshPro") || Contains(shaderName, "TMP_SDF")) return ShaderFamily.TmpSdf;
            if (Contains(shaderName, "Toon") || Contains(shaderName, "CelShading") || Contains(shaderName, "Avatar")) return ShaderFamily.Toon;
            if (shaderName.StartsWith("DCL/Scene", StringComparison.Ordinal)) return ShaderFamily.UrpLit;
            if (Contains(shaderName, "Skybox") || Contains(shaderName, "GenesisSky")) return ShaderFamily.Skybox;
            if (Contains(shaderName, "Particle")) return ShaderFamily.Particles;
            if (Contains(shaderName, "UUAV") || Contains(shaderName, "Video")) return ShaderFamily.Video;

            if (Contains(shaderName, "Terrain") || Contains(shaderName, "Foliage") || Contains(shaderName, "Flowers") || Contains(shaderName, "Grass")
                || Contains(shaderName, "Tree") || Contains(shaderName, "Rock") || Contains(shaderName, "Cliff") || Contains(shaderName, "Ocean")
                || Contains(shaderName, "Water") || Contains(shaderName, "GPUInstancer") || Contains(shaderName, "Billboard"))
                return ShaderFamily.Landscape;

            if (shaderName.StartsWith("UI/", StringComparison.Ordinal) || Contains(shaderName, "/UI/") || Contains(shaderName, "Sprites") || Contains(shaderName, "SoftMask"))
                return ShaderFamily.Ui;

            if (Contains(shaderName, "Unlit")) return ShaderFamily.Unlit;
            if (Contains(shaderName, "/Lit") || Contains(shaderName, "Lit (") || shaderName.EndsWith("Lit", StringComparison.Ordinal)) return ShaderFamily.UrpLit;
            if (shaderName.StartsWith("Hidden/", StringComparison.Ordinal)) return ShaderFamily.Internal;

            return ShaderFamily.Unassigned;
        }

        private static bool Contains(string name, string token) =>
            name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
