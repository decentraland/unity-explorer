using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Base for the two shader-tuning preset types (DCL_Toon_Studio / DCL_Stylized_PBR). Stores knob
    /// values by property name rather than fixed fields (unlike CardColorPreset's 7 named colours)
    /// since the knob tables in StudioAvatarShaderSwitcher can grow or change — applying a preset
    /// just skips any entry the current knob table no longer declares. Editor-only tooling asset,
    /// same as CardColorPreset: lives in the Editor assembly, nothing ships to a build.
    ///
    /// Concrete subclasses (StudioToonShaderPreset, StudioPbrShaderPreset) each live in their own
    /// file matching the class name — Unity only resolves a ScriptableObject's serialized script
    /// reference (m_Script) correctly for the type whose name matches its containing file, so a
    /// second class stashed in this file would save with a broken (fileID: 0) script reference and
    /// silently vanish from AssetDatabase.FindAssets("t:...") queries.
    /// </summary>
    public abstract class StudioShaderPreset : ScriptableObject
    {
        [Serializable]
        public class FloatEntry
        {
            public string property;
            public float value;
        }

        [Serializable]
        public class ColorEntry
        {
            public string property;
            public Color value;
        }

        public List<FloatEntry> floats = new();
        public List<ColorEntry> colors = new();
    }
}
