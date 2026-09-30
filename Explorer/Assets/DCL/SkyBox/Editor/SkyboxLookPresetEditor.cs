using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Preset inspector organised the way the sky is authored: a phase palette table (every colour role at each
    ///     phase anchor, editing the matching gradient keys in place), a per-phase hue/saturation/brightness shift, the
    ///     regular fields with the ones the active mode ignores folded away, and the sky lookup bake.
    /// </summary>
    [CustomEditor(typeof(SkyboxLookPreset))]
    public class SkyboxLookPresetEditor : UnityEditor.Editor
    {
        private const int LUT_WIDTH = 256;
        private const string LUT_SUFFIX = "_SkyLut.asset";

        // A gradient key this close to a phase anchor is that anchor's key.
        private const float KEY_TOLERANCE = 0.02f;
        private const int MAX_GRADIENT_KEYS = 8;
        private const float LABEL_WIDTH = 130f;
        private const float CELL_MIN_WIDTH = 60f;

        private static readonly string[] PHASES = { "Night", "Sunrise", "Day", "Sunset" };
        private static readonly float[] PHASE_TIMES = { 0f, 0.25f, 0.5f, 0.75f };
        private static readonly string[] SKY_FIELDS = { "skyNight", "skySunrise", "skyDay", "skySunset" };

        private static readonly (string Label, string Field)[] PALETTE_ROWS =
        {
            ("Cloud lit", "cloudsColorRamp"),
            ("Cloud shadow", "cloudsShadowColorRamp"),
            ("Sun disc", "sunColorRamp"),
            ("Light", "directionalColorRamp"),
            ("Ambient sky", "indirectSkyRamp"),
            ("Ambient equator", "indirectEquatorRamp"),
            ("Ambient ground", "groundEquatorRamp"),
            ("Fog", "fogColorRamp"),
        };

        // Fields that do nothing while the sky lookup is on: the legacy band sky, rim, stars and cubemap clouds.
        private static readonly HashSet<string> LUT_SUPERSEDED = new ()
        {
            "skyZenitColorRamp", "skyHorizonColorRamp", "skyNadirColorRamp", "rimColorRamp", "cloudsHighlightsIntensity",
            "zenitSpread", "zenitBlend", "groundSpread", "groundBlend", "blendTwist", "rimSpread", "rimOpacity",
            "starsBrightness", "starsTexture", "cloudsCubemap", "cloudOpacity", "cloudsRotationSpeed",
        };

        // Fields the computed celestial path ignores.
        private static readonly HashSet<string> COMPUTED_SUPERSEDED = new () { "sunOpacity", "moonMaskSize", "moonMaskPosition" };

        private readonly List<string> hiddenFields = new ();

        private bool showPalette = true;
        private bool showShift;
        private bool showHidden;
        private bool lutDirty;
        private int shiftPhase;
        private float hueShift;
        private float saturationScale = 1f;
        private float brightnessScale = 1f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool lutOn = serializedObject.FindProperty("useSkyLut").boolValue;
            bool computed = serializedObject.FindProperty("computeCelestialPath").boolValue;

            if (lutOn)
            {
                DrawPalette();
                DrawShift();
                DrawBake();
                EditorGUILayout.Space();
            }

            DrawFields(lutOn, computed);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawBake();
        }

        // Bake button with its stale-LUT warning.
        private void DrawBake()
        {
            if (lutDirty)
                EditorGUILayout.HelpBox("Sky gradients changed: bake the sky LUT to see them.", MessageType.Warning);

            if (GUILayout.Button("Bake sky LUT"))
            {
                BakeSkyLut((SkyboxLookPreset)target, serializedObject);
                lutDirty = false;
            }
        }

        /// <summary>
        ///     One column per phase anchor. The sky row edits the whole colour-over-elevation gradient of that phase;
        ///     every other row edits the gradient key sitting on the anchor (Night also writes the wrapped key at 1).
        /// </summary>
        private void DrawPalette()
        {
            showPalette = EditorGUILayout.Foldout(showPalette, "Phase palette", true, EditorStyles.foldoutHeader);

            if (!showPalette)
                return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(string.Empty, GUILayout.Width(LABEL_WIDTH));

            for (var i = 0; i < PHASES.Length; i++)
                GUILayout.Label(PHASES[i], EditorStyles.centeredGreyMiniLabel, GUILayout.MinWidth(CELL_MIN_WIDTH));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Sky (horizon to zenith)", "Colour over elevation for the phase; the lookup is baked from these."), GUILayout.Width(LABEL_WIDTH));

            for (var i = 0; i < SKY_FIELDS.Length; i++)
            {
                SerializedProperty sky = serializedObject.FindProperty(SKY_FIELDS[i]);
                EditorGUI.BeginChangeCheck();
                Gradient edited = EditorGUILayout.GradientField(GUIContent.none, sky.gradientValue, true, GUILayout.MinWidth(CELL_MIN_WIDTH));

                if (EditorGUI.EndChangeCheck())
                {
                    sky.gradientValue = edited;
                    lutDirty = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            for (var row = 0; row < PALETTE_ROWS.Length; row++)
            {
                SerializedProperty ramp = serializedObject.FindProperty(PALETTE_ROWS[row].Field);
                Gradient gradient = ramp.gradientValue;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(PALETTE_ROWS[row].Label, GUILayout.Width(LABEL_WIDTH));

                for (var i = 0; i < PHASE_TIMES.Length; i++)
                {
                    EditorGUI.BeginChangeCheck();
                    Color edited = EditorGUILayout.ColorField(GUIContent.none, gradient.Evaluate(PHASE_TIMES[i]), false, false, true, GUILayout.MinWidth(CELL_MIN_WIDTH));

                    if (EditorGUI.EndChangeCheck())
                    {
                        SetAnchorKey(gradient, PHASE_TIMES[i], edited);
                        ramp.gradientValue = gradient;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            DrawFogDensityRow();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("moonColorRamp"), new GUIContent("Moon disc (rise to set)", "On the moon's own progress, not the phase."));
        }

        // Exponential fog density per anchor, the one scalar row of the table.
        private void DrawFogDensityRow()
        {
            SerializedProperty density = serializedObject.FindProperty("fogDensityByPhase");
            Vector4 values = density.vector4Value;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Fog density", "Exponential fog density; 0.0005 is about 2 km to the 1/e point."), GUILayout.Width(LABEL_WIDTH));

            EditorGUI.BeginChangeCheck();

            for (var i = 0; i < PHASE_TIMES.Length; i++)
                values[i] = Mathf.Max(0f, EditorGUILayout.FloatField(GUIContent.none, values[i], GUILayout.MinWidth(CELL_MIN_WIDTH)));

            if (EditorGUI.EndChangeCheck())
                density.vector4Value = values;

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        ///     Recolours one phase at once: every palette key on that anchor plus every key of that phase's sky gradient.
        /// </summary>
        private void DrawShift()
        {
            showShift = EditorGUILayout.Foldout(showShift, "Shift a phase", true, EditorStyles.foldoutHeader);

            if (!showShift)
                return;

            shiftPhase = EditorGUILayout.Popup("Phase", shiftPhase, PHASES);
            hueShift = EditorGUILayout.Slider("Hue shift (degrees)", hueShift, -180f, 180f);
            saturationScale = EditorGUILayout.Slider("Saturation x", saturationScale, 0f, 2f);
            brightnessScale = EditorGUILayout.Slider("Brightness x", brightnessScale, 0f, 3f);

            bool identity = Mathf.Approximately(hueShift, 0f) && Mathf.Approximately(saturationScale, 1f) && Mathf.Approximately(brightnessScale, 1f);
            bool apply;

            using (new EditorGUI.DisabledScope(identity))
                apply = GUILayout.Button($"Apply to {PHASES[shiftPhase]}");

            if (!apply)
                return;

            float anchor = PHASE_TIMES[shiftPhase];

            for (var row = 0; row < PALETTE_ROWS.Length; row++)
            {
                SerializedProperty ramp = serializedObject.FindProperty(PALETTE_ROWS[row].Field);
                Gradient gradient = ramp.gradientValue;
                GradientColorKey[] keys = gradient.colorKeys;

                for (var k = 0; k < keys.Length; k++)
                    if (IsAnchorKey(keys[k].time, anchor))
                        keys[k].color = Shift(keys[k].color);

                gradient.colorKeys = keys;
                ramp.gradientValue = gradient;
            }

            SerializedProperty sky = serializedObject.FindProperty(SKY_FIELDS[shiftPhase]);
            Gradient skyGradient = sky.gradientValue;
            GradientColorKey[] skyKeys = skyGradient.colorKeys;

            for (var k = 0; k < skyKeys.Length; k++)
                skyKeys[k].color = Shift(skyKeys[k].color);

            skyGradient.colorKeys = skyKeys;
            sky.gradientValue = skyGradient;
            lutDirty = true;

            hueShift = 0f;
            saturationScale = 1f;
            brightnessScale = 1f;
        }

        /// <summary>
        ///     The default fields in declaration order, minus the ones the active mode ignores, which go into a
        ///     collapsed foldout at the end so they stay reachable.
        /// </summary>
        private void DrawFields(bool lutOn, bool computed)
        {
            hiddenFields.Clear();
            SerializedProperty property = serializedObject.GetIterator();
            var enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property);

                    continue;
                }

                bool hidden = (lutOn && LUT_SUPERSEDED.Contains(property.name)) || (computed && COMPUTED_SUPERSEDED.Contains(property.name));

                if (hidden)
                {
                    hiddenFields.Add(property.name);
                    continue;
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(property, true);

                if (EditorGUI.EndChangeCheck() && Array.IndexOf(SKY_FIELDS, property.name) >= 0)
                    lutDirty = true;
            }

            if (hiddenFields.Count == 0)
                return;

            EditorGUILayout.Space();
            showHidden = EditorGUILayout.Foldout(showHidden, $"Inactive in this mode ({hiddenFields.Count})", true, EditorStyles.foldoutHeader);

            if (!showHidden)
                return;

            EditorGUILayout.HelpBox("Legacy band sky, rim, stars and cubemap clouds are compiled out while Use Sky Lut is on; Sun Opacity, Moon Mask Size and Moon Mask Position are ignored by the computed celestial path.", MessageType.None);

            for (var i = 0; i < hiddenFields.Count; i++)
                EditorGUILayout.PropertyField(serializedObject.FindProperty(hiddenFields[i]), true);
        }

        // Night sits at both ends of a ramp, so its anchor also owns the key at 1.
        private static bool IsAnchorKey(float keyTime, float anchor) =>
            Mathf.Abs(keyTime - anchor) <= KEY_TOLERANCE || (anchor == 0f && keyTime >= 1f - KEY_TOLERANCE);

        /// <summary>
        ///     Writes the colour into the key on the anchor, adding one when the ramp has none there and room for it,
        ///     otherwise moving the nearest key onto the anchor.
        /// </summary>
        private static void SetAnchorKey(Gradient gradient, float anchor, Color color)
        {
            GradientColorKey[] keys = gradient.colorKeys;
            var onAnchor = false;
            var onWrap = false;

            for (var k = 0; k < keys.Length; k++)
            {
                if (!IsAnchorKey(keys[k].time, anchor)) continue;

                keys[k].color = color;

                if (Mathf.Abs(keys[k].time - anchor) <= KEY_TOLERANCE)
                    onAnchor = true;
                else
                    onWrap = true;
            }

            // A recoloured wrapped key alone is not enough: phase 0 still reads the first key, so the anchor key is added below.
            if (onAnchor)
            {
                gradient.colorKeys = keys;
                return;
            }

            if (keys.Length < MAX_GRADIENT_KEYS)
            {
                // Night owns both ends of the ramp, so it gets the wrapped key at 1 as well when there is room and none exists.
                bool wrapNight = anchor == 0f && !onWrap && keys.Length + 1 < MAX_GRADIENT_KEYS;
                var grown = new GradientColorKey[keys.Length + (wrapNight ? 2 : 1)];
                keys.CopyTo(grown, 0);
                grown[keys.Length] = new GradientColorKey(color, anchor);

                if (wrapNight)
                    grown[keys.Length + 1] = new GradientColorKey(color, 1f);

                Array.Sort(grown, (a, b) => a.time.CompareTo(b.time));
                gradient.colorKeys = grown;
                return;
            }

            var nearest = 0;

            for (var k = 1; k < keys.Length; k++)
                if (Mathf.Abs(keys[k].time - anchor) < Mathf.Abs(keys[nearest].time - anchor))
                    nearest = k;

            keys[nearest].time = anchor;
            keys[nearest].color = color;
            gradient.colorKeys = keys;
        }

        // Hue, saturation and brightness on an HDR colour; the intensity above 1 rides in V.
        private Color Shift(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            h = Mathf.Repeat(h + (hueShift / 360f), 1f);
            s = Mathf.Clamp01(s * saturationScale);
            v *= brightnessScale;
            Color shifted = Color.HSVToRGB(h, s, v, true);
            shifted.a = color.a;
            return shifted;
        }

        private static void BakeSkyLut(SkyboxLookPreset preset, SerializedObject serializedPreset)
        {
            Gradient[] rows = { preset.SkyNight, preset.SkySunrise, preset.SkyDay, preset.SkySunset, preset.SkyNight };

            var baked = new Texture2D(LUT_WIDTH, rows.Length, TextureFormat.RGBAHalf, false, true)
            {
                name = preset.name + "_SkyLut",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[LUT_WIDTH * rows.Length];

            for (var y = 0; y < rows.Length; y++)
            for (var x = 0; x < LUT_WIDTH; x++)
                pixels[(y * LUT_WIDTH) + x] = rows[y].Evaluate(x / (LUT_WIDTH - 1f));

            baked.SetPixels(pixels);
            baked.Apply(false, false);

            string presetPath = AssetDatabase.GetAssetPath(preset);
            string lutPath = Path.ChangeExtension(presetPath, null) + LUT_SUFFIX;
            Texture2D? existing = AssetDatabase.LoadAssetAtPath<Texture2D>(lutPath);

            if (existing != null)
            {
                EditorUtility.CopySerialized(baked, existing);
                DestroyImmediate(baked);
                baked = existing;
            }
            else
                AssetDatabase.CreateAsset(baked, lutPath);

            serializedPreset.FindProperty("skyLut").objectReferenceValue = baked;
            serializedPreset.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
