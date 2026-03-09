using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Configs
{
    /// <summary>
    /// Describes the emoji sprite atlas used by the situational reaction particle system.
    /// At runtime this asset is the single source of truth: the controller reads Atlas,
    /// TileSizePx, and FlipY from here and applies them to the material's shader properties
    /// (_AtlasTex, _AtlasCols, _AtlasRows, _FlipY). Any values previously baked into the
    /// material will be overwritten on initialization.
    /// </summary>
    [CreateAssetMenu(fileName = "ChatReactionsAtlasConfig",
                     menuName = "DCL/Chat/Reactions/Atlas Config")]
    public class ChatReactionsAtlasConfig : ScriptableObject
    {
        [field: Tooltip("The emoji sprite sheet. Must be readable (Read/Write enabled in import settings).")]
        [field: SerializeField] public Texture2D Atlas { get; private set; }

        [field: Min(1)]
        [field: Tooltip("Side length of a single emoji tile in the atlas (pixels).")]
        [field: SerializeField] public int TileSizePx { get; private set; } = 32;

        [field: Tooltip("Flip atlas rows so row 0 is at the bottom of the texture. " +
                        "Enable when your texture importer has 'Flip Green Channel' or stores rows top-to-bottom.")]
        [field: SerializeField] public bool FlipY { get; private set; } = true;

        [field: Header("Unicode Mapping")]
        [field: Tooltip("TMP Sprite Asset used by the emoji panel. Required for mapping Unicode emoji " +
                        "to atlas tile indices when adding reactions from the emoji panel.")]
        [field: SerializeField] public TMP_SpriteAsset SpriteAsset { get; private set; }

        public int Cols => Atlas != null ? Mathf.Max(1, Atlas.width / TileSizePx) : 1;
        public int Rows => Atlas != null ? Mathf.Max(1, Atlas.height / TileSizePx) : 1;
        public int TotalTiles => Cols * Rows;

        private Dictionary<uint, int>? unicodeToTileIndex;

        /// <summary>
        /// Maps a Unicode codepoint to an atlas tile index using the TMP sprite asset's glyph table.
        /// Returns -1 if the codepoint is not found in the atlas.
        /// </summary>
        public int GetTileIndexFromUnicode(uint unicodeCodepoint)
        {
            if (SpriteAsset == null) return -1;

            if (unicodeToTileIndex == null)
            {
                var chars = SpriteAsset.spriteCharacterTable;
                unicodeToTileIndex = new Dictionary<uint, int>(chars.Count);
                for (int i = 0; i < chars.Count; i++)
                    unicodeToTileIndex[chars[i].unicode] = (int)chars[i].glyphIndex;
            }

            return unicodeToTileIndex.TryGetValue(unicodeCodepoint, out int index) ? index : -1;
        }

        /// <summary>
        /// Logs every Unicode → tile-index mapping from the sprite asset to the console.
        /// Available via the context menu (three-dot menu) in the Inspector.
        /// </summary>
        [ContextMenu("Log All Unicode → Tile Mappings")]
        public void LogAllMappings()
        {
#if UNITY_EDITOR
            if (SpriteAsset == null)
            {
                Debug.LogWarning($"[{name}] SpriteAsset is null — no mappings to log.");
                return;
            }

            var chars = SpriteAsset.spriteCharacterTable;
            var sb = new System.Text.StringBuilder(chars.Count * 32);
            sb.AppendLine($"[{name}] Atlas mappings ({chars.Count} entries):");

            for (int i = 0; i < chars.Count; i++)
            {
                uint unicode = chars[i].unicode;
                int glyphIdx = (int)chars[i].glyphIndex;

                string emoji;

                if (unicode >= 0xD800 && unicode <= 0xDFFF || unicode > 0x10FFFF)
                    emoji = "?";
                else if (unicode <= 0xFFFF)
                    emoji = ((char)unicode).ToString();
                else
                    emoji = char.ConvertFromUtf32((int)unicode);

                sb.AppendLine($"  [{i}] U+{unicode:X4} ({emoji}) → tile {glyphIdx}");
            }

            Debug.Log(sb.ToString());
#endif
        }

        public Rect GetUVRect(int tileIndex)
        {
            int col = tileIndex % Cols;
            int row = tileIndex / Cols;

            if (FlipY)
                row = Rows - 1 - row;

            float w = 1f / Cols;
            float h = 1f / Rows;

            return new Rect(col * w, row * h, w, h);
        }
    }
}
