using DCL.Rendering.DCL_Toon;
using UnityEngine;

public static class CommonAssets
{
    public static Material AvatarMaterial { get; set; }
    public static Material FacialFeaturesMaterial { get; set; }

    // Shared stylized-metallic matcap library (lives in the unity-shared-dependencies package).
    // Assigned once at boot; read by ToonMaterialGenerator to bind the default matcap, and by the
    // LocalWearableOverride test tool to resolve a matcap by name.
    public static MatcapPresets MatcapPresets { get; set; }

    // Name of the preset ToonMaterialGenerator binds by default to metallic materials.
    public static string DefaultMatcapName { get; set; }
}