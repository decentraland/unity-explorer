using System;
using UnityEngine;

namespace DCL.Lobby
{
    /// <summary>
    ///     A set piece placed on the stage by a preset. Position is relative to the stage origin, except Y which is measured
    ///     from the floor surface so a prop resting on the ground is at Y = 0.
    /// </summary>
    [Serializable]
    public class LobbyStageProp
    {
        [field: SerializeField] public GameObject? Prefab { get; private set; }
        [field: SerializeField] public Vector3 Position { get; private set; }
        [field: SerializeField] public Vector3 Rotation { get; private set; }
        [field: SerializeField] public Vector3 Scale { get; private set; } = Vector3.one;

        // A list entry added in the inspector is zero-filled, ignoring the initialiser; a zero scale is never what was meant
        internal void SanitizeDefaults()
        {
            if (Scale == Vector3.zero)
                Scale = Vector3.one;
        }
    }

    /// <summary>
    ///     Everything on the lobby stage that changes with the backdrop image: the image itself, where and how the floor dissolves
    ///     into it, which part of the image is visible, the floor and mist tints that sit with it, and the set pieces around it.
    /// </summary>
    [CreateAssetMenu(fileName = "LobbyStagePreset", menuName = "DCL/Lobby/Stage Preset")]
    public class LobbyStagePreset : ScriptableObject
    {
        [field: SerializeField] public Texture2D? Backdrop { get; private set; }

        [field: Header("Blend")]
        [field: Tooltip("Screen height, from the bottom, where the floor has fully dissolved into the backdrop")]
        [field: SerializeField, Range(0.05f, 0.9f)] public float BlendScreenHeight { get; private set; } = 0.35f;

        [field: Tooltip("Length of the dissolve on the floor, in metres towards the camera from the blend line")]
        [field: SerializeField, Min(0.1f)] public float BlendDepth { get; private set; } = 4f;

        [field: Tooltip("How abrupt the dissolve is inside the band: 0 is a smooth gradient over the whole depth, 1 is a hard cut at its middle")]
        [field: SerializeField, Range(0f, 1f)] public float BlendHardness { get; private set; }

        [field: Tooltip("Fraction of the image height kept below the blend line, so the visible band starts higher up the image")]
        [field: SerializeField, Range(0f, 0.9f)] public float ImageBelowBlend { get; private set; } = 0.35f;

        [field: Header("Colours")]
        [field: SerializeField] public Color FloorColor { get; private set; } = new (0.55f, 0.55f, 0.58f, 1f);
        [field: SerializeField] public Color SpotColor { get; private set; } = new (0.15f, 0.13f, 0.2f, 1f);
        [field: SerializeField] public Color MistColor { get; private set; } = new (0.6f, 0.5f, 0.8f, 0.35f);

        [field: Tooltip("Fraction of the image height, from the bottom, sampled by 'Match colours to backdrop'")]
        [field: SerializeField, Range(0.02f, 0.5f)] public float ColorSampleBand { get; private set; } = 0.15f;

        [field: Header("Key Light")]
        [field: Tooltip("Euler angles of the stage's directional light")]
        [field: SerializeField] public Vector3 LightRotation { get; private set; } = new (25f, 95f, 0f);
        [field: SerializeField] public Color LightColor { get; private set; } = new (1f, 0.859f, 0.404f, 1f);
        [field: SerializeField, Min(0f)] public float LightIntensity { get; private set; } = 2f;

        [field: Header("Vignette")]
        [field: Tooltip("How dark the rim of the frame gets; 0 turns the vignette off")]
        [field: SerializeField, Range(0f, 1f)] public float VignetteIntensity { get; private set; }

        [field: Tooltip("How far in from the corners the darkening reaches: small values keep it to a thin rim, 1 fades it all the way to the centre")]
        [field: SerializeField, Range(0.05f, 1f)] public float VignetteSmoothness { get; private set; } = 0.6f;

        [field: SerializeField] public Color VignetteColor { get; private set; } = Color.black;

        [field: Header("Props")]
        [field: SerializeField] public LobbyStageProp[] Props { get; private set; } = Array.Empty<LobbyStageProp>();

        /// <summary>
        ///     Bumped on every inspector edit, so a stage can tell when its spawned props are stale without comparing the list.
        /// </summary>
        public int Version { get; private set; }

#if UNITY_EDITOR
        /// <summary>
        ///     Raised after any preset asset is edited in the inspector, so stages showing it can refresh without waiting for a render.
        /// </summary>
        public static event Action<LobbyStagePreset>? AnyChanged;

        private void OnValidate()
        {
            foreach (LobbyStageProp prop in Props)
                prop.SanitizeDefaults();

            Version++;
            AnyChanged?.Invoke(this);
        }

        private const int SAMPLE_SIZE = 64;
        private const int HISTOGRAM_LEVELS = 16;
        private const float DOMINANT_WEIGHT = 0.7f;

        // The floor stays darker and calmer than the image so the ground texture reads; the spot lifts it; the mist sits between
        private const float FLOOR_SATURATION = 0.6f;
        private const float FLOOR_VALUE = 0.55f;
        private const float SPOT_SATURATION = 0.8f;
        private const float SPOT_VALUE = 0.75f;
        private const float MIST_SATURATION = 0.7f;
        private const float MIST_VALUE = 0.85f;

        [ContextMenu("Match colours to backdrop")]
        private void MatchColorsToBackdrop()
        {
            if (Backdrop == null) return;

            Color dominant = DominantBottomColor(Backdrop, ColorSampleBand);
            Color.RGBToHSV(dominant, out float hue, out float saturation, out float value);

            UnityEditor.Undo.RecordObject(this, "Match colours to backdrop");
            FloorColor = Color.HSVToRGB(hue, saturation * FLOOR_SATURATION, value * FLOOR_VALUE);
            SpotColor = Color.HSVToRGB(hue, saturation * SPOT_SATURATION, Mathf.Min(value * SPOT_VALUE, 1f));

            Color mist = Color.HSVToRGB(hue, saturation * MIST_SATURATION, Mathf.Min(value * MIST_VALUE, 1f));
            mist.a = MistColor.a;
            MistColor = mist;

            UnityEditor.EditorUtility.SetDirty(this);
        }

        // Blits only the bottom band into a small texture (UV origin is bottom-left, so no orientation guessing), then blends
        // the most populated colour bin with the plain average so one bright detail cannot take over
        private static Color DominantBottomColor(Texture texture, float band)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(SAMPLE_SIZE, SAMPLE_SIZE, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, renderTexture, new Vector2(1f, band), Vector2.zero);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var readable = new Texture2D(SAMPLE_SIZE, SAMPLE_SIZE, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, SAMPLE_SIZE, SAMPLE_SIZE), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);

            Color32[] pixels = readable.GetPixels32();
            DestroyImmediate(readable);

            int shift = 8 - (int)Mathf.Log(HISTOGRAM_LEVELS, 2);
            var counts = new int[HISTOGRAM_LEVELS * HISTOGRAM_LEVELS * HISTOGRAM_LEVELS];
            var sums = new Vector3[counts.Length];
            var total = Vector3.zero;

            foreach (Color32 pixel in pixels)
            {
                var rgb = new Vector3(pixel.r, pixel.g, pixel.b);
                int bin = ((pixel.r >> shift) * HISTOGRAM_LEVELS * HISTOGRAM_LEVELS) + ((pixel.g >> shift) * HISTOGRAM_LEVELS) + (pixel.b >> shift);
                counts[bin]++;
                sums[bin] += rgb;
                total += rgb;
            }

            int best = 0;

            for (var i = 1; i < counts.Length; i++)
                if (counts[i] > counts[best]) best = i;

            Vector3 dominant = sums[best] / (counts[best] * 255f);
            Vector3 average = total / (pixels.Length * 255f);
            Vector3 result = Vector3.Lerp(average, dominant, DOMINANT_WEIGHT);

            return new Color(result.x, result.y, result.z, 1f);
        }
#endif
    }
}
