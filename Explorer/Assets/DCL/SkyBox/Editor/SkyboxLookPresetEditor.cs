using System.IO;
using UnityEditor;
using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Bakes the preset's four colour-over-elevation gradients into the sky lookup texture the shader samples.
    ///     The texture is saved next to the preset and re-used on later bakes so references stay valid.
    /// </summary>
    [CustomEditor(typeof(SkyboxLookPreset))]
    public class SkyboxLookPresetEditor : UnityEditor.Editor
    {
        private const int LUT_WIDTH = 256;
        private const string LUT_SUFFIX = "_SkyLut.asset";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (GUILayout.Button("Bake sky LUT"))
                BakeSkyLut((SkyboxLookPreset)target, serializedObject);
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
