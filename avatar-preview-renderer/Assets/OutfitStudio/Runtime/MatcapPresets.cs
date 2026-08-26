// DELETE at package integration time — this is a verbatim copy of
// unity-shared-dependencies' Runtime/Shaders/Avatar/DCL_Toon/Runtime/MatcapPresets.cs
// (branch feat/toon-normalmap-stylized-metallic @ 9eda18fb). Once that branch is merged and the
// package repointed, this duplicate type collides with the package one (CS0433 will flag it):
// delete this file + Assets/OutfitStudio/Shaders/Matcaps/, and wire Bootstrap per the metallic
// branch instead of StudioAvatarShaderSwitcher's matcap bootstrap.
using System;
using UnityEngine;

namespace DCL.Rendering.DCL_Toon
{
    /// <summary>
    /// Shared, ordered library of stylized-metallic matcap presets for the DCL_Toon shader. This is
    /// the single source of truth across consuming apps (aang-renderer, unity-explorer, ...): each app
    /// pulls this asset from the package instead of shipping its own matcap textures.
    ///
    /// Wearables reference a matcap by its stable <see cref="Preset.name"/> in their JSON; consumers
    /// resolve that name to an array index with <see cref="TryGetIndex"/>. That index is the contract
    /// for the shader's <c>_MatCap_SamplerArr_ID</c> (texture-array path) and the slice in a matcap
    /// <c>Texture2DArray</c> — so the ORDER of presets is significant and must stay stable once
    /// wearables reference them. The non-array path (aang) binds <see cref="Preset.texture"/> directly
    /// to <c>_MatCap_Sampler</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "DCL/Toon/Matcap Presets", fileName = "MatcapPresets")]
    public class MatcapPresets : ScriptableObject
    {
        [Serializable]
        public struct Preset
        {
            [Tooltip("Stable identifier referenced by wearable JSON. Must be unique and must not " +
                     "change once wearables reference it.")]
            public string name;

            public Texture2D texture;

            [Tooltip("Optional per-matcap tint applied to the shader's _MatCapColor. White = no tint.")]
            public Color tint;

            [Tooltip("Optional per-matcap blur applied to the shader's _BlurLevelMatcap (matcap mip LOD).")]
            [Range(0f, 8f)] public float blur;
        }

        [SerializeField] private Preset[] presets;

        /// <summary>Number of presets in the library.</summary>
        public int Count => presets?.Length ?? 0;

        /// <summary>The preset at a given slice index (the shader's _MatCap_SamplerArr_ID value).</summary>
        public Preset this[int index] => presets[index];

        /// <summary>
        /// Resolves a preset name to its array index — i.e. the shader slice id. Case-sensitive.
        /// Returns false (index = -1) when the name is unknown, so callers can fall back gracefully.
        /// </summary>
        public bool TryGetIndex(string presetName, out int index)
        {
            if (presets != null)
            {
                for (var i = 0; i < presets.Length; i++)
                {
                    if (presets[i].name == presetName)
                    {
                        index = i;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

        /// <summary>Resolves a preset name to the preset itself.</summary>
        public bool TryGet(string presetName, out Preset preset)
        {
            if (TryGetIndex(presetName, out var index))
            {
                preset = presets[index];
                return true;
            }

            preset = default;
            return false;
        }

        private void OnValidate()
        {
            if (presets == null) return;

            // A freshly-added inspector element defaults every field to zero; a zero (transparent
            // black) tint would zero out the matcap contribution, which is never intended, so treat
            // an unset tint as "no tint" = white. Intentional non-white tints are preserved.
            for (var i = 0; i < presets.Length; i++)
            {
                if (presets[i].tint == default)
                    presets[i].tint = Color.white;
            }
        }
    }
}
