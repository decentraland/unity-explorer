using System;
using DCL.Rendering.DCL_Toon;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OutfitStudio.Editor
{
    public enum StudioShaderMode
    {
        DclToon = 0,
        DclToonStudio = 1,
        DclStylizedPbr = 2,
        DclEmotes = 3
    }

    /// <summary>
    /// How the window renders a knob. Toggle is stored and pushed exactly like a Float (0 or 1) —
    /// it only changes the control, so presets and the material push need no special case.
    /// </summary>
    public enum StudioKnobKind { Float, Color, Toggle }

    /// <summary>
    /// A single art-direction control exposed in the window's shader tuning panel and pushed onto
    /// every avatar material of the active shader. These are global look knobs (rim, ambient,
    /// stylization) — deliberately NOT per-wearable identity data (textures, base color, gates).
    /// </summary>
    public sealed class StudioShaderKnob
    {
        public readonly string Label;
        public readonly string Property;
        public readonly int PropId;
        public readonly StudioKnobKind Kind;
        public readonly float Min, Max, Default;
        public readonly Color DefaultColor;
        public readonly string Tooltip;

        public StudioShaderKnob(string label, string property, float min, float max, float def, string tooltip = null)
        {
            Label = label;
            Property = property;
            PropId = Shader.PropertyToID(property);
            Kind = StudioKnobKind.Float;
            Min = min;
            Max = max;
            Default = def;
            Tooltip = tooltip;
        }

        public StudioShaderKnob(string label, string property, bool def, string tooltip = null)
        {
            Label = label;
            Property = property;
            PropId = Shader.PropertyToID(property);
            Kind = StudioKnobKind.Toggle;
            Min = 0f;
            Max = 1f;
            Default = def ? 1f : 0f;
            Tooltip = tooltip;
        }

        public StudioShaderKnob(string label, string property, Color def, string tooltip = null)
        {
            Label = label;
            Property = property;
            PropId = Shader.PropertyToID(property);
            Kind = StudioKnobKind.Color;
            DefaultColor = def;
            Tooltip = tooltip;
        }
    }

    /// <summary>
    /// Enforces the Outfit Studio's selected avatar shader on every avatar material, in edit AND
    /// play mode, across reloads, and pushes the art-direction tuning knobs onto them. Poll-based
    /// like the other studio helpers: every avatar reload creates fresh material clones with the
    /// stock DCL/DCL_Toon shader, and the next tick swaps them back to the selected shader and
    /// re-applies the tuning — no loader hooks needed.
    ///
    /// Swap notes: named properties survive material.shader reassignment, but renderQueue resets
    /// to the new shader's default (the generator sets it explicitly for cutout/transparent
    /// wearables) and keywords are restored defensively — both are saved around the swap.
    /// Facial features (DCL/DCL_Avatar_Facial_Features) are excluded by the shader-name filter —
    /// their shader is never swapped. DCL_Emotes is the one mode that still has an opinion about
    /// them: it hides those renderers outright (see IsFacialFeature) so the white body stays blank.
    ///
    /// Also bootstraps CommonAssets.MatcapPresets (the metallic branch wires this in Bootstrap;
    /// the studio does it here to keep Bootstrap/Main.unity untouched).
    /// </summary>
    [InitializeOnLoad]
    public static class StudioAvatarShaderSwitcher
    {
        private const string EDITOR_PREFS_KEY = "OutfitStudio.Shader";
        private const string MATCAP_KEY = "OutfitStudio.Matcap";

        /// <summary>Prefix for the per-camera stash of the authored volume mask — see ApplyPostBypass.</summary>
        private const string VOLUME_MASK_KEY = "OutfitStudio.EmotesVolumeMask.";
        private const string MATCAP_PRESETS_PATH = "Assets/OutfitStudio/Shaders/Matcaps/MatcapPresets.asset";
        private const string DEFAULT_MATCAP_NAME = "matcap_01";

        private const string SHADER_TOON = "DCL/DCL_Toon";
        private const string SHADER_STUDIO = "DCL/DCL_Toon_Studio";
        private const string SHADER_PBR = "DCL/DCL_Stylized_PBR";
        private const string SHADER_EMOTES = "DCL/DCL_Emotes";

        /// <summary>
        /// Facial features (eyes/eyebrows/mouth) are separate renderers on their own shader, which
        /// this switcher never swaps — see the note in the class summary. DCL_Emotes hides them
        /// instead, so the name is needed here too.
        /// </summary>
        private const string SHADER_FACIAL = "DCL/DCL_Avatar_Facial_Features";

        /// <summary>
        /// What emote props are built on — GLTFLoader.LoadEmote hands its importer a
        /// DecentralandMaterialGenerator("DCL/Scene"), so a prop is scene geometry, not an avatar
        /// material, and normally sits outside this switcher's swap set entirely.
        /// </summary>
        private const string SHADER_SCENE = "DCL/Scene";

        /// <summary>The GameObject GLTFLoader.LoadEmote parents every prop under, by that literal
        /// name. It is the only marker a prop instance carries — see IsEmoteProp.</summary>
        private const string EMOTE_PROP_ROOT = "emote";

        private static double _nextCheck;
        private static string _warnedMissingShader;

        // Metal-gate diagnostic property ids (see the verbose dump in Apply).
        private static readonly int IsStylizedMetallicId = Shader.PropertyToID("_IsStylizedMetallic");
        private static readonly int MatcapArrId = Shader.PropertyToID("_MatCap_SamplerArr_ID");
        private static readonly int MatcapSamplerId = Shader.PropertyToID("_MatCap_Sampler");
        private static readonly int MetallicGlossArrId = Shader.PropertyToID("_MetallicGlossMapArr_ID");
        private static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
        private static readonly int StylizedMetalStrengthId = Shader.PropertyToID("_StylizedMetalStrength");
        private static readonly int MatcapMetalBlendId = Shader.PropertyToID("_MatcapMetalBlend");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        // --- Tuning knobs (single source of truth: the window builds sliders from these) --------

        // DCL_Toon_Studio default rim tint: warm gold #CCB777 (RGB 204,183,119). PBR used to share it,
        // but its rim is now a near-white (see PbrRimNeutral) — the two shaders tint their rims from
        // different lighting, so they no longer track each other.
        private static readonly Color RimGold = new(204f / 255f, 183f / 255f, 119f / 255f);

        // DCL_Toon_Studio default outline tint: burnt orange #B85C2A (RGB 184,92,42). PBR outlines are
        // plain black, so this is toon-only now.
        private static readonly Color OutlineOrange = new(184f / 255f, 92f / 255f, 42f / 255f);

        // DCL_Stylized_PBR default rim tint: near-white #F1F1F1 (RGB 241,241,241). Almost but not quite
        // white — it takes its warmth from the scene's key light rather than from the tint.
        private static readonly Color PbrRimNeutral = new(241f / 255f, 241f / 255f, 241f / 255f);

        /// <summary>DCL_Toon_Studio — the knobs unlocked over the stock toon shader.</summary>
        public static readonly StudioShaderKnob[] StudioKnobs =
        {
            new("Rim Intensity", "_RimLightIntensity", 0f, 10f, 10f, "Overall strength of the rim/back light band."),
            new("Rim Power", "_RimLight_Power", 0f, 1f, 0.8f, "Rim falloff/width — higher wraps further onto the front."),
            new("Rim Inside Mask", "_RimLight_InsideMask", 0f, 0.95f, 0.5f, "Pushes the rim toward the silhouette edge."),
            new("Rim Color", "_RimLightColor", RimGold, "Rim tint."),
            new("Ambient (GI)", "_GI_Intensity", 0f, 2f, 0f, "Flat ambient fill from the environment SH."),
            new("Normal Strength", "_BumpScale", 0f, 2f, 1f, "Global normal-map intensity (overrides per-wearable scale)."),
            new("Metal Strength", "_StylizedMetalStrength", 0f, 1f, 1f, "Blend of the matcap metallic reflection."),
            new("Matcap Tint", "_MatCapColor", Color.white, "Colors the matcap metal reflection (white = untinted)."),
            new("Matcap Blur", "_BlurLevelMatcap", 0f, 4f, 0f, "Softens the matcap reflection (mip LOD)."),
            new("Outline Width", "_Outline_Width", 0f, 10f, 3f, "Thickness of the avatar outline. Stock is 2; wider survives the camera's antialiasing instead of being eroded into the background."),
            new("Outline Color", "_Outline_Color", OutlineOrange, "Flat color of the avatar outline.")
        };

        /// <summary>DCL_Stylized_PBR — the full principled + stylization control set.</summary>
        public static readonly StudioShaderKnob[] PbrKnobs =
        {
            new("Rim Intensity", "_RimLightIntensity", 0f, 4f, 0.36f, "Overall strength of the fresnel rim."),
            new("Rim Power", "_RimLight_Power", 0f, 1f, 0.694f, "Rim falloff/width."),
            new("Rim Inside Mask", "_RimLight_InsideMask", 0f, 0.95f, 0.774f, "Pushes the rim toward the silhouette edge."),
            new("Rim Sharpness", "_RimSharpness", 0f, 1f, 1f, "0 = soft gradient rim, 1 = hard band."),
            new("Rim Color", "_RimLightColor", PbrRimNeutral, "Rim tint."),
            new("Diffuse Wrap", "_DiffuseWrap", 0f, 1f, 0.815f, "Wraps light past the terminator for softer shading."),
            new("Shadow Sharpness", "_ShadowSharpness", 0f, 1f, 0.943f, "0 = smooth lambert, 1 = hard two-tone break."),
            new("Specular Softness", "_SpecularSoftness", 0f, 4f, 3.95f, "Compresses the highlight into a broad stylized gleam."),
            new("Specular (F0)", "_Specular", 0f, 1f, 0.506f, "Dielectric reflectance (non-metal surfaces)."),
            new("Sheen", "_Sheen", 0f, 1f, 0.72f, "Cloth-like grazing-edge gleam."),
            new("Sheen Tint", "_SheenTint", 0f, 1f, 0.143f, "White vs albedo-tinted sheen."),
            new("Clearcoat", "_Clearcoat", 0f, 1f, 0f, "Glossy secondary coat (the action-figure finish)."),
            new("Clearcoat Gloss", "_ClearcoatGloss", 0f, 1f, 0.539f, "Sharpness of the clearcoat lobe."),
            new("Ambient (GI)", "_GI_Intensity", 0f, 5f, 5f, "Flat ambient fill from the environment SH."),
            new("Emission Strength", "_EmissionStrength", 0f, 2f, 0.03f, "Scales emissive output. PBR emissives bloom hotter than toon, so this sits far below the toon shader's own emissive level."),
            new("Matcap Metal Blend", "_MatcapMetalBlend", 0f, 1f, 0.48f, "0 = physical edge-only reflection (dark front), 1 = flat matcap that matches DCL_Toon_Studio chrome."),
            new("Metal Strength", "_StylizedMetalStrength", 0f, 4f, 0.02f, "How strongly the matcap replaces the metal surface (1 = full, matches toon; >1 over-drives)."),
            new("Matcap Tint", "_MatCapColor", Color.white, "Colors the matcap metal reflection (white = untinted)."),
            new("Matcap Blur", "_BlurLevelMatcap", 0f, 4f, 4f, "Softens the matcap reflection (mip LOD)."),
            new("Normal Strength", "_BumpScale", 0f, 2f, 1.11f, "Global normal-map intensity (overrides per-wearable scale)."),
            new("Outline Width", "_Outline_Width", 0f, 10f, 1.89f, "Thickness of the avatar outline. Thinner than the toon shader's 3 — the PBR look leans on shading rather than on a heavy contour."),
            new("Outline Color", "_Outline_Color", Color.black, "Flat color of the avatar outline.")
        };

        /// <summary>
        /// DCL_Emotes — outline, plus one behavior knob. The avatar surface is a flat white with no
        /// lighting model, so the contour is the entire art direction there; anything else would be
        /// a knob with nothing to act on. Wider than the other two shaders' defaults because the
        /// white body gives the stroke no tonal contrast to lean on. "Use Emote shader on props" is
        /// the exception — it decides whether emote props are flattened to that same white or keep
        /// their own PBR/scene look (see Apply).
        /// </summary>
        public static readonly StudioShaderKnob[] EmotesKnobs =
        {
            new("Outline Width", "_Outline_Width", 0f, 10f, 5f, "Thickness of the avatar outline."),
            new("Outline Color", "_Outline_Color", Color.black, "Flat color of the avatar outline."),
            new("Outline As Mask", "_OutlineAsMask", true,
                "Cut the outline out of the image instead of painting it, so the card shows through it " +
                "— around the silhouette AND along the lines between wearables. Needs the Card Frame on " +
                "to have something to reveal; with it off the cut is a plain hole, which exports " +
                "transparent but only reads as black in the Game view."),
            new("Outline Detail Suppress", "_Outline_DetailSuppress", 0f, 1f, 0.33f,
                "Drops the outline wherever the surface creases too sharply to read as a clean line " +
                "— fingers, face wrinkles — instead of drawing broken/noisy dashes there. 0 = off " +
                "(every silhouette edge draws); raise it until the noise clears without eating a " +
                "real silhouette edge."),
            new("Use Emote shader on props", "_UseEmoteShaderOnProps", false,
                "When on, emote props are flattened to the same white DCL_Emotes shader as the " +
                "avatar. When off (default), props keep rendering with their own PBR/scene shader " +
                "and textures — how it worked before props were flattened to white.")
        };

        private static readonly StudioShaderKnob OutlineMaskKnob =
            Array.Find(EmotesKnobs, k => k.Property == "_OutlineAsMask");

        /// <summary>Backs the "Use Emote shader on props" knob — see <see cref="Apply"/>.</summary>
        private static readonly StudioShaderKnob UseEmoteShaderOnPropsKnob =
            Array.Find(EmotesKnobs, k => k.Property == "_UseEmoteShaderOnProps");

        /// <summary>
        /// True while DCL_Emotes is selected with its "Outline As Mask" knob on. Read by
        /// StudioCardFrame, which has to drop the card panel's depth test for the card to be
        /// visible through the cut — see the note on the card layer there.
        /// </summary>
        public static bool EmotesOutlineMask =>
            Mode == StudioShaderMode.DclEmotes &&
            GetFloat(StudioShaderMode.DclEmotes, OutlineMaskKnob) > 0.5f;

        public static StudioShaderKnob[] KnobsFor(StudioShaderMode mode) => mode switch
        {
            StudioShaderMode.DclToonStudio => StudioKnobs,
            StudioShaderMode.DclStylizedPbr => PbrKnobs,
            StudioShaderMode.DclEmotes => EmotesKnobs,
            _ => Array.Empty<StudioShaderKnob>()
        };

        public static StudioShaderMode Mode
        {
            get => (StudioShaderMode)EditorPrefs.GetInt(EDITOR_PREFS_KEY, (int)StudioShaderMode.DclToon);
            set
            {
                EditorPrefs.SetInt(EDITOR_PREFS_KEY, (int)value);
                Apply(verbose: true); // user clicked a button — report the outcome
            }
        }

        /// <summary>
        /// The matcap preset (by name) bound to stylized-metal materials. Applies globally in the
        /// studio: newly generated materials pick it up via CommonAssets.DefaultMatcapName, and the
        /// poll pushes it onto every already-loaded metal material so a change is live. Persisted.
        /// </summary>
        public static string ActiveMatcapName
        {
            get => EditorPrefs.GetString(MATCAP_KEY, DEFAULT_MATCAP_NAME);
            set
            {
                EditorPrefs.SetString(MATCAP_KEY, value);
                CommonAssets.DefaultMatcapName = value; // future ApplyDefaultMatcap() calls use it
                Apply();
            }
        }

        /// <summary>Preset names from the loaded matcap library (empty until it's bootstrapped).</summary>
        public static string[] GetMatcapNames()
        {
            var presets = CommonAssets.MatcapPresets;
            if (presets == null || presets.Count == 0) return Array.Empty<string>();
            var names = new string[presets.Count];
            for (var i = 0; i < presets.Count; i++) names[i] = presets[i].name;
            return names;
        }

        static StudioAvatarShaderSwitcher()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += _ => EnsureMatcapPresets();
        }

        // --- Knob value storage (per shader mode; rim power for toon vs pbr are independent) -----

        private static string KnobKey(StudioShaderMode mode, StudioShaderKnob knob)
            => $"OutfitStudio.Knob.{(int)mode}.{knob.Property}";

        public static float GetFloat(StudioShaderMode mode, StudioShaderKnob knob)
            => EditorPrefs.GetFloat(KnobKey(mode, knob), knob.Default);

        public static Color GetColor(StudioShaderMode mode, StudioShaderKnob knob)
        {
            var s = EditorPrefs.GetString(KnobKey(mode, knob), null);
            if (!string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString(s, out var c)) return c;
            return knob.DefaultColor;
        }

        public static void SetFloat(StudioShaderMode mode, StudioShaderKnob knob, float value)
        {
            EditorPrefs.SetFloat(KnobKey(mode, knob), value);
            Apply();
        }

        public static void SetColor(StudioShaderMode mode, StudioShaderKnob knob, Color value)
        {
            EditorPrefs.SetString(KnobKey(mode, knob), "#" + ColorUtility.ToHtmlStringRGBA(value));
            Apply();
        }

        public static void ResetKnobs(StudioShaderMode mode)
        {
            foreach (var knob in KnobsFor(mode))
                EditorPrefs.DeleteKey(KnobKey(mode, knob));
            Apply();
        }

        // --- Presets (save/apply the full knob set to/from a ScriptableObject) -------------------

        /// <summary>Snapshot of every knob's current value for <paramref name="mode"/> into a preset,
        /// used by the window's "Save current…" preset button.</summary>
        public static void CaptureKnobValues(StudioShaderMode mode, StudioShaderPreset preset)
        {
            foreach (var knob in KnobsFor(mode))
            {
                if (knob.Kind == StudioKnobKind.Color)
                    preset.colors.Add(new StudioShaderPreset.ColorEntry { property = knob.Property, value = GetColor(mode, knob) });
                else
                    preset.floats.Add(new StudioShaderPreset.FloatEntry { property = knob.Property, value = GetFloat(mode, knob) });
            }
        }

        /// <summary>Applies a saved preset's values onto the live knobs for <paramref name="mode"/>.
        /// Entries for knobs the current knob table no longer declares are ignored.</summary>
        public static void ApplyPreset(StudioShaderMode mode, StudioShaderPreset preset)
        {
            var knobs = KnobsFor(mode);

            foreach (var entry in preset.floats)
            {
                var knob = Array.Find(knobs, k => k.Kind != StudioKnobKind.Color && k.Property == entry.property);
                if (knob != null) EditorPrefs.SetFloat(KnobKey(mode, knob), entry.value);
            }

            foreach (var entry in preset.colors)
            {
                var knob = Array.Find(knobs, k => k.Kind == StudioKnobKind.Color && k.Property == entry.property);
                if (knob != null) EditorPrefs.SetString(KnobKey(mode, knob), "#" + ColorUtility.ToHtmlStringRGBA(entry.value));
            }

            Apply();
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 0.5;

            // Cleared here rather than in Apply, which early-outs before it could: leaving the
            // studio scene has to hand the renderer's production behaviour back. Same shape as
            // StudioCardFrame's OutlineSuppressed line.
            if (SceneManager.GetActiveScene().path != OutfitStudioWindow.STUDIO_SCENE_PATH)
            {
                Loading.AvatarLoader.OutlineEmoteProps = false;
                return;
            }

            EnsureMatcapPresets();
            Apply();
        }

        public static void Apply() => Apply(false);

        /// <summary>
        /// Swaps every avatar material in the studio scene to the selected shader and pushes the
        /// tuning knobs onto it. Uses Resources.FindObjectsOfTypeAll so it reaches the edit-mode
        /// preview's HideFlags.DontSave renderers (FindObjectsByType would miss them) regardless of
        /// hierarchy, and covers play-mode wearables the same way; filtered to the active scene so
        /// project assets are never touched. Idempotent. When <paramref name="verbose"/>, logs the
        /// outcome (used on button clicks so a no-op is never silent).
        /// </summary>
        public static void Apply(bool verbose)
        {
            if (SceneManager.GetActiveScene().path != OutfitStudioWindow.STUDIO_SCENE_PATH)
            {
                if (verbose)
                    Debug.LogWarning("[OutfitStudio] Shader switching only runs in the studio scene " +
                                     $"({OutfitStudioWindow.STUDIO_SCENE_PATH}). Active scene is " +
                                     $"\"{SceneManager.GetActiveScene().path}\".");
                return;
            }

            var mode = Mode;

            var isEmotes = mode == StudioShaderMode.DclEmotes;

            // Off unless DCL_Emotes is active AND the "Use Emote shader on props" knob is on —
            // see the knob's tooltip. Off by default so props keep their own PBR/scene look, which
            // is how the tool behaved before props were flattened to white.
            var useEmoteShaderOnProps = isEmotes &&
                GetFloat(StudioShaderMode.DclEmotes, UseEmoteShaderOnPropsKnob) > 0.5f;

            // Both before the shader swap, and before the Shader.Find early-out below, so a
            // missing shader can never strand the camera or the renderer in the mode's state.
            ApplyPostBypass(isEmotes);

            // Props are only flattened to the same white as the avatar when the knob above is on,
            // and only then do they need the contour to stay separable from it. Off in every other
            // mode/knob state (and off outside the studio scene — see Update), which is what keeps
            // production behaviour unchanged.
            Loading.AvatarLoader.OutlineEmoteProps = useEmoteShaderOnProps;

            var targetName = mode switch
            {
                StudioShaderMode.DclToonStudio => SHADER_STUDIO,
                StudioShaderMode.DclStylizedPbr => SHADER_PBR,
                StudioShaderMode.DclEmotes => SHADER_EMOTES,
                _ => SHADER_TOON
            };

            var target = Shader.Find(targetName);
            if (target == null)
            {
                // Not imported yet, or a shader compile error left it unresolvable — surface it
                // once instead of silently doing nothing.
                if (_warnedMissingShader != targetName)
                {
                    Debug.LogWarning($"[OutfitStudio] Shader \"{targetName}\" not found — shader switch " +
                                     "skipped. Check the Console for shader compile errors, or reimport " +
                                     "Assets/OutfitStudio/Shaders.");
                    _warnedMissingShader = targetName;
                }
                return;
            }
            _warnedMissingShader = null;

            // Only needed to hand emote props back when leaving DCL_Emotes. Resolved once, and
            // tolerated as null: the swap that needs it simply doesn't happen.
            var sceneShader = Shader.Find(SHADER_SCENE);

            var knobs = KnobsFor(mode);
            var activeScene = SceneManager.GetActiveScene();

            // Resolve the selected matcap preset once — pushed onto every metal material below so the
            // window's matcap dropdown is live (the generator only binds it at material-creation time).
            var presets = CommonAssets.MatcapPresets;
            var haveMatcap = presets != null && presets.Count > 0;
            var activeMatcap = default(MatcapPresets.Preset);
            if (haveMatcap && !presets.TryGet(ActiveMatcapName, out activeMatcap))
                activeMatcap = presets[0];

            var avatarMats = 0;
            var swapped = 0;
            var metalDiag = verbose ? new System.Text.StringBuilder() : null;

            // Resources.FindObjectsOfTypeAll finds EVERY loaded renderer including HideFlags.DontSave
            // ones (the edit-mode preview uses that flag; FindObjectsByType would miss them) and
            // inactive ones — independent of avatar hierarchy. Filter to the studio scene so we never
            // touch project assets, prefab-stage objects, or other scenes.
            foreach (var renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (renderer.gameObject.scene != activeScene) continue;

                // DCL_Emotes wants a blank white mannequin, but facial features are their own
                // renderers on a shader this switcher never swaps, so they'd keep drawing eyes and
                // brows onto the white body. Hide them for the duration of the mode instead.
                // forceRenderingOff rather than .enabled: it is never serialized (the scene stays
                // clean, unlike every other write in this file) and it is a separate channel, so
                // whatever else disabled the renderer stays authoritative when we release it.
                if (IsFacialFeature(renderer))
                {
                    if (renderer.forceRenderingOff != isEmotes) renderer.forceRenderingOff = isEmotes;
                    continue;
                }

                var isProp = IsEmoteProp(renderer);

                // sharedMaterials (never .material — that leaks instances in edit mode)
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null || mat.shader == null) continue;

                    var name = mat.shader.name;

                    // Emote props aren't avatar materials, so they're normally none of this
                    // switcher's business. DCL_Emotes with "Use Emote shader on props" on is the
                    // exception: a prop still wearing its own textures beside a flat white avatar
                    // reads as a bug. Unlike the avatar swap this one has to go BACK, and the rule
                    // is symmetric — DCL/Scene out, DCL/Scene in — so it needs no saved state that a
                    // domain reload could lose.
                    string wantName;
                    if (isProp && (name == SHADER_SCENE || name == SHADER_EMOTES))
                        wantName = useEmoteShaderOnProps ? SHADER_EMOTES : SHADER_SCENE;
                    else if (name == SHADER_TOON || name == SHADER_STUDIO || name == SHADER_PBR ||
                             name == SHADER_EMOTES)
                        wantName = targetName;
                    else
                        continue;

                    // Never touch persisted assets (Avatar_Toon.mat, the preview platform's
                    // Platform_MAT — the project's only authored DCL/Scene material). Avatar and
                    // prop materials are always runtime clones, so this only skips misconfigured
                    // edge cases.
                    if (EditorUtility.IsPersistent(mat)) continue;

                    avatarMats++;

                    if (name != wantName)
                    {
                        var want = wantName == SHADER_SCENE ? sceneShader : target;
                        if (want != null)
                        {
                            var queue = mat.renderQueue;
                            var keywords = mat.shaderKeywords;
                            mat.shader = want;
                            mat.shaderKeywords = keywords;
                            mat.renderQueue = queue;
                            swapped++;
                        }
                    }

                    // Everything below is about the selected avatar shader. A prop on its way back
                    // to DCL/Scene has none of these properties and wants none of this tuning.
                    if (wantName != targetName) continue;

                    // Push the current art-direction values (no-op for stock toon: it has no knobs)
                    foreach (var knob in knobs)
                    {
                        // Colour is the odd one out; Float and Toggle are both plain floats.
                        if (knob.Kind == StudioKnobKind.Color)
                            mat.SetColor(knob.PropId, GetColor(mode, knob));
                        else
                            mat.SetFloat(knob.PropId, GetFloat(mode, knob));
                    }

                    // Re-assert the stylized-metal gate flag. The material is born on the stock
                    // DCL/DCL_Toon package shader, which on this branch does NOT declare the
                    // metallic-branch property _IsStylizedMetallic. Setting a real Integer property
                    // the active shader doesn't declare doesn't survive the later mat.shader swap to
                    // the studio shader — it falls back to the shader default (0) — so the generator's
                    // _IsStylizedMetallic=1 is lost and the shader's `_IsStylizedMetallic > 0` gate
                    // never opens (metal invisible, though normals — never gated on it — still show).
                    // The mask id _MetallicGlossMapArr_ID DOES survive (>= 0 when the generator
                    // detected metal, -1 otherwise), so use it as the "metal was detected" signal and
                    // re-set the flag now that the active studio shader actually declares it.
                    if (mat.HasProperty(IsStylizedMetallicId) && mat.HasProperty(MetallicGlossArrId))
                        mat.SetInteger(IsStylizedMetallicId, mat.GetInteger(MetallicGlossArrId) >= 0 ? 1 : 0);

                    // Push the selected matcap TEXTURE onto metal materials (those the generator flagged
                    // with a mask, _MetallicGlossMapArr_ID >= 0) so the window's dropdown switches it
                    // live. Tint (_MatCapColor) and blur (_BlurLevelMatcap) are deliberately NOT set
                    // here — they're tuning knobs now (pushed by the knob loop above), so the preset
                    // only supplies the texture. Non-metal materials keep the gate shut, so they're
                    // left alone.
                    if (haveMatcap && mat.HasProperty(MatcapSamplerId) &&
                        mat.HasProperty(MetallicGlossArrId) && mat.GetInteger(MetallicGlossArrId) >= 0)
                    {
                        mat.SetTexture(MatcapSamplerId, activeMatcap.texture);
                        if (mat.HasProperty(MatcapArrId)) mat.SetInteger(MatcapArrId, 0);
                    }

                    // DIAGNOSTIC (verbose only): dump the metal-gate state per material so we can see
                    // which condition fails — detection (_IsStylizedMetallic), the matcap gate
                    // (_MatCap_SamplerArr_ID >= 0 + a bound _MatCap_Sampler), or the mask id.
                    // Every read is HasProperty-guarded — properties differ between the toon/PBR
                    // shaders, and an unguarded Get logs an error and returns 0.
                    if (metalDiag != null)
                    {
                        string I(int id) => mat.HasProperty(id) ? mat.GetInteger(id).ToString() : "n/a";
                        string F(int id) => mat.HasProperty(id) ? mat.GetFloat(id).ToString("0.##") : "n/a";
                        string T(int id) => !mat.HasProperty(id) ? "n/a" : (mat.GetTexture(id) != null ? "SET" : "null");
                        metalDiag.AppendLine(
                            $"    • {mat.name}: _IsStylizedMetallic={I(IsStylizedMetallicId)} " +
                            $"_MatCap_SamplerArr_ID={I(MatcapArrId)} " +
                            $"_MatCap_Sampler={T(MatcapSamplerId)} " +
                            $"_MetallicGlossMapArr_ID={I(MetallicGlossArrId)} " +
                            $"_MetallicGlossMap={T(MetallicGlossMapId)} " +
                            $"_StylizedMetalStrength={F(StylizedMetalStrengthId)} " +
                            $"_MatcapMetalBlend={F(MatcapMetalBlendId)} " +
                            $"_Metallic={F(MetallicId)}");
                    }
                }
            }

            if (verbose)
            {
                if (avatarMats == 0)
                    Debug.LogWarning($"[OutfitStudio] {targetName}: 0 avatar materials in the scene — " +
                                     "load an outfit into the preview (or enter play mode) first, then click again.");
                else
                    Debug.Log($"[OutfitStudio] {targetName}: {avatarMats} avatar material(s), {swapped} swapped.\n" +
                              $"Metal gate diagnostic (MatcapPresets={(CommonAssets.MatcapPresets != null ? CommonAssets.MatcapPresets.Count + " presets" : "NULL")}):\n" +
                              metalDiag);
            }

            if (swapped > 0)
            {
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        /// <summary>
        /// Makes the studio cameras ignore the scene's volume stack while DCL_Emotes is selected,
        /// and hands it back the moment another shader is picked.
        ///
        /// Why the shader can't do this on its own: a surface that writes 1.0 lands on 212/255
        /// after the profile's ACES tonemapping, and Bloom's soft knee (threshold 1, so the curve
        /// starts contributing at 0.5) haloes it. Both are volume overrides, so a volumeLayerMask
        /// of 0 drops the whole stack back to defaults — Tonemapping None, Bloom intensity 0,
        /// ShadowsMidtonesHighlights identity — and 1.0 finally reaches 255,255,255.
        ///
        /// Deliberately NOT renderPostProcessing=false: post has to keep running so SMAA still
        /// resolves the outline, which is thin enough to matter (that is also why the mode's
        /// default outline width is 4).
        ///
        /// The authored mask is stashed in EditorPrefs rather than a static so the restore
        /// survives a domain reload, and keyed per camera so two cameras with different masks
        /// each get their own back. Note this affects the whole frame, not just the avatar: the
        /// background and card frame lose ACES too for as long as the mode is on.
        /// </summary>
        private static void ApplyPostBypass(bool bypass)
        {
            var activeScene = SceneManager.GetActiveScene();

            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.gameObject.scene != activeScene) continue;

                var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData == null) continue;

                var key = VOLUME_MASK_KEY + camera.name;

                if (bypass)
                {
                    // Already bypassed (or authored that way) — nothing to save, and saving a 0
                    // here would be how we'd lose the real mask.
                    if (cameraData.volumeLayerMask.value == 0) continue;

                    EditorPrefs.SetInt(key, cameraData.volumeLayerMask.value);
                    cameraData.volumeLayerMask = 0;
                }
                else
                {
                    // No key means the current mask is the camera's own, not ours to overwrite.
                    if (!EditorPrefs.HasKey(key)) continue;

                    cameraData.volumeLayerMask = EditorPrefs.GetInt(key);
                    EditorPrefs.DeleteKey(key);
                }
            }
        }

        /// <summary>
        /// True for renderers inside an emote's prop instance. GLTFLoader.LoadEmote parents the
        /// whole prop under a GameObject it literally names "emote"; that name is the only marker
        /// the prop carries, and nothing else in the studio scene sits under one.
        /// </summary>
        private static bool IsEmoteProp(Renderer renderer)
        {
            for (var t = renderer.transform; t != null; t = t.parent)
                if (t.name == EMOTE_PROP_ROOT)
                    return true;

            return false;
        }

        private static bool IsFacialFeature(Renderer renderer)
        {
            foreach (var mat in renderer.sharedMaterials)
                if (mat != null && mat.shader != null && mat.shader.name == SHADER_FACIAL)
                    return true;

            return false;
        }

        /// <summary>
        /// The metallic branch assigns the matcap library in Bootstrap; the studio assigns it here
        /// so the renderer's scene stays untouched. Runs cheaply on the poll (also after the
        /// play-mode domain reload wipes the statics).
        /// </summary>
        private static void EnsureMatcapPresets()
        {
            if (CommonAssets.MatcapPresets != null) return;

            CommonAssets.MatcapPresets = AssetDatabase.LoadAssetAtPath<MatcapPresets>(MATCAP_PRESETS_PATH);
            CommonAssets.DefaultMatcapName = ActiveMatcapName; // honor the window's selection for new materials
        }
    }
}
