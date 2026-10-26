using DCL.Diagnostics;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Seasonal snow on the GLTF meshes of a few Genesis City scenes (the Genesis Plaza winter event), driven by
    ///     <see cref="PlazaSnowConfig" />. Renderers that get snow carry <see cref="RENDERING_LAYER_MASK" />; the
    ///     <c>DCL/Scene</c> shader covers their upward-facing surfaces while the global <c>_DCL_SNOW</c> keyword is on.
    /// </summary>
    public class PlazaSnow
    {
        // Must match SNOW_RENDERING_LAYER_MASK in Scene_Snow.hlsl
        public const uint RENDERING_LAYER_MASK = 1u << 8;

        private const float DEFAULT_AMOUNT = 1f;
        private const float DEFAULT_COVERAGE_START = 0.55f;
        private const float DEFAULT_COVERAGE_FALLOFF = 0.25f;
        private const float DEFAULT_TILING = 0.5f;
        private const float DEFAULT_SMOOTHNESS = 0.35f;
        private const float DEFAULT_NORMAL_STRENGTH = 1f;
        private const float DEFAULT_EDGE_NOISE = 0.25f;
        private const float DEFAULT_EDGE_NOISE_FREQUENCY = 0.6f;
        private const float DEFAULT_BUMP_STRENGTH = 0.06f;
        private const float DEFAULT_BUMP_FREQUENCY = 2f;
        private const float DEFAULT_SPARKLE_INTENSITY = 6f;
        private const float DEFAULT_SPARKLE_DENSITY = 0.15f;
        private const float DEFAULT_SPARKLE_SIZE = 0.03f;
        private const float DEFAULT_SPARKLE_SHARPNESS = 300f;

        private static readonly Color DEFAULT_COLOR = new (0.95f, 0.97f, 1f);
        private static readonly GlobalKeyword SNOW_KEYWORD = GlobalKeyword.Create("_DCL_SNOW");
        private static readonly int ALBEDO_MAP_ID = Shader.PropertyToID("_DCLSnowAlbedoMap");
        private static readonly int NORMAL_MAP_ID = Shader.PropertyToID("_DCLSnowNormalMap");
        private static readonly int COLOR_ID = Shader.PropertyToID("_DCLSnowColor");
        private static readonly int PARAMS_ID = Shader.PropertyToID("_DCLSnowParams");
        private static readonly int SURFACE_ID = Shader.PropertyToID("_DCLSnowSurface");
        private static readonly int DETAIL_ID = Shader.PropertyToID("_DCLSnowDetail");
        private static readonly int SPARKLE_ID = Shader.PropertyToID("_DCLSnowSparkle");

        private readonly HashSet<Vector2Int> baseParcels = new ();
        private readonly HashSet<string> excludedGltfs = new (StringComparer.OrdinalIgnoreCase);
        private readonly PlazaSnowConfig config;

        public PlazaSnow(PlazaSnowConfig config)
        {
            this.config = config;

            if (config.baseParcels != null)
                foreach (string parcel in config.baseParcels)
                {
                    if (TryParseParcel(parcel, out Vector2Int parsed))
                        baseParcels.Add(parsed);
                    else
                        ReportHub.LogWarning(ReportCategory.GLTF_CONTAINER, $"Plaza snow: ignoring base parcel \"{parcel}\", expected \"x,y\"");
                }

            if (config.excludedGltfs != null)
                foreach (string src in config.excludedGltfs)
                    excludedGltfs.Add(src);
        }

        public bool IncludesScene(Vector2Int baseParcel) =>
            baseParcels.Contains(baseParcel);

        /// <summary>
        ///     Adds or removes the snow rendering layer on every renderer of the asset. Removal matters too: the asset may
        ///     come from the GLTF cache after being used by a scene with snow.
        /// </summary>
        public void TagRenderers(GltfContainerAsset asset, string src, bool sceneHasSnow)
        {
            bool snow = sceneHasSnow && !excludedGltfs.Contains(src);

            for (var i = 0; i < asset.Renderers.Count; i++)
            {
                Renderer renderer = asset.Renderers[i];
                uint mask = renderer.renderingLayerMask;
                uint newMask = snow ? mask | RENDERING_LAYER_MASK : mask & ~RENDERING_LAYER_MASK;

                if (newMask != mask)
                    renderer.renderingLayerMask = newMask;
            }
        }

        public void EnableShaderGlobals()
        {
            Color color = DEFAULT_COLOR;

            if (config.color != null && !ColorUtility.TryParseHtmlString(config.color, out color))
            {
                ReportHub.LogWarning(ReportCategory.GLTF_CONTAINER, $"Plaza snow: ignoring color \"{config.color}\", expected an HTML color");
                color = DEFAULT_COLOR;
            }

            // Flat white and flat normal until dedicated snow textures are provided
            Shader.SetGlobalTexture(ALBEDO_MAP_ID, Texture2D.whiteTexture);
            Shader.SetGlobalTexture(NORMAL_MAP_ID, Texture2D.normalTexture);
            Shader.SetGlobalColor(COLOR_ID, color);

            Shader.SetGlobalVector(PARAMS_ID, new Vector4(
                config.amount ?? DEFAULT_AMOUNT,
                config.coverageStart ?? DEFAULT_COVERAGE_START,
                config.coverageFalloff ?? DEFAULT_COVERAGE_FALLOFF,
                config.tiling ?? DEFAULT_TILING));

            Shader.SetGlobalVector(SURFACE_ID, new Vector4(
                config.smoothness ?? DEFAULT_SMOOTHNESS,
                config.normalStrength ?? DEFAULT_NORMAL_STRENGTH,
                config.edgeNoise ?? DEFAULT_EDGE_NOISE,
                config.edgeNoiseFrequency ?? DEFAULT_EDGE_NOISE_FREQUENCY));

            Shader.SetGlobalVector(DETAIL_ID, new Vector4(
                config.bumpStrength ?? DEFAULT_BUMP_STRENGTH,
                config.bumpFrequency ?? DEFAULT_BUMP_FREQUENCY,
                0f,
                0f));

            Shader.SetGlobalVector(SPARKLE_ID, new Vector4(
                config.sparkleIntensity ?? DEFAULT_SPARKLE_INTENSITY,
                config.sparkleDensity ?? DEFAULT_SPARKLE_DENSITY,
                config.sparkleSize ?? DEFAULT_SPARKLE_SIZE,
                config.sparkleSharpness ?? DEFAULT_SPARKLE_SHARPNESS));

            Shader.EnableKeyword(in SNOW_KEYWORD);
        }

        public void DisableShaderGlobals()
        {
            Shader.DisableKeyword(in SNOW_KEYWORD);
        }

#if UNITY_EDITOR
        private const string FORCE_IN_EDITOR_PREF = "DCL.PlazaSnow.ForceInEditor";
        private const string FORCE_IN_EDITOR_MENU = "Decentraland/Plaza Snow/Force In Editor";
        private const string GENESIS_PLAZA_BASE_PARCEL = "-3,-2";

        /// <summary>
        ///     Snow on Genesis Plaza with the default look, for Play mode without the feature flag. Opt-in per machine
        ///     through the menu toggle.
        /// </summary>
        public static PlazaSnowConfig? EditorConfig() =>
            UnityEditor.EditorPrefs.GetBool(FORCE_IN_EDITOR_PREF)
                ? new PlazaSnowConfig { baseParcels = new[] { GENESIS_PLAZA_BASE_PARCEL } }
                : null;

        [UnityEditor.MenuItem(FORCE_IN_EDITOR_MENU)]
        private static void ToggleForceInEditor()
        {
            UnityEditor.EditorPrefs.SetBool(FORCE_IN_EDITOR_PREF, !UnityEditor.EditorPrefs.GetBool(FORCE_IN_EDITOR_PREF));
        }

        [UnityEditor.MenuItem(FORCE_IN_EDITOR_MENU, true)]
        private static bool ValidateForceInEditor()
        {
            UnityEditor.Menu.SetChecked(FORCE_IN_EDITOR_MENU, UnityEditor.EditorPrefs.GetBool(FORCE_IN_EDITOR_PREF));
            return true;
        }
#endif

        private static bool TryParseParcel(string value, out Vector2Int parcel)
        {
            parcel = default;
            int comma = value.IndexOf(',');

            if (comma < 0
                || !int.TryParse(value.AsSpan(0, comma), out int x)
                || !int.TryParse(value.AsSpan(comma + 1), out int y))
                return false;

            parcel = new Vector2Int(x, y);
            return true;
        }
    }
}
