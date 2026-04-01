using System.Collections.Generic;
using DCL.Chat.ChatReactions.Core;
using DCL.Diagnostics;
using TMPro;
using UnityEngine;
using Utility;

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
        [field: Note("The emoji sprite sheet texture. Must have Read/Write enabled in import settings.")]
        [field: SerializeField] public Texture2D Atlas { get; private set; }

        [field: Note("Side length of one emoji tile in the atlas (pixels). " +
                     "Cols and Rows are derived from atlas dimensions / tile size.")]
        [field: Range(8, 128)]
        [field: SerializeField] public int TileSizePx { get; private set; } = 32;

        [field: Note("Flip atlas rows so row 0 is at the bottom. " +
                     "Enable when the texture stores rows top-to-bottom (most PNG exports do).")]
        [field: SerializeField] public bool FlipY { get; private set; } = true;

        [field: Header("Unicode Mapping")]
        [field: Note("TMP Sprite Asset used by the emoji panel. Maps Unicode codepoints to atlas tile indices. " +
                     "Use context menu 'Log All Unicode → Tile Mappings' to inspect.")]
        [field: SerializeField] public TMP_SpriteAsset SpriteAsset { get; private set; }

        public int Cols => Atlas != null ? Mathf.Max(1, Atlas.width / TileSizePx) : 1;
        public int Rows => Atlas != null ? Mathf.Max(1, Atlas.height / TileSizePx) : 1;
        public int TotalTiles => Cols * Rows;

        private Dictionary<uint, int>? unicodeToTileIndex;
        private Dictionary<int, uint>? tileIndexToUnicode;

        /// <summary>
        /// Maps a Unicode codepoint to an atlas tile index using the TMP sprite asset's glyph table.
        /// Returns -1 if the codepoint is not found in the atlas.
        /// </summary>
        public int GetTileIndexFromUnicode(uint unicodeCodepoint)
        {
            if (SpriteAsset == null) return -1;

            EnsureLookupTablesBuilt();

            return unicodeToTileIndex!.GetValueOrDefault(unicodeCodepoint, -1);
        }

        /// <summary>
        /// Reverse lookup: maps an atlas tile index back to its Unicode codepoint.
        /// Returns 0 if the tile index is not found.
        /// </summary>
        public uint GetUnicodeFromTileIndex(int tileIndex)
        {
            if (SpriteAsset == null) return 0;

            EnsureLookupTablesBuilt();

            return tileIndexToUnicode!.GetValueOrDefault(tileIndex, 0u);
        }

        private void EnsureLookupTablesBuilt()
        {
            if (unicodeToTileIndex != null) return;

            var chars = SpriteAsset.spriteCharacterTable;
            unicodeToTileIndex = new Dictionary<uint, int>(chars.Count);
            tileIndexToUnicode = new Dictionary<int, uint>(chars.Count);

            for (int i = 0; i < chars.Count; i++)
            {
                uint unicode = chars[i].unicode;
                int glyphIdx = (int)chars[i].glyphIndex;
                unicodeToTileIndex[unicode] = glyphIdx;
                tileIndexToUnicode[glyphIdx] = unicode;
            }
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
                ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES,$"[{name}] SpriteAsset is null — no mappings to log.");
                return;
            }

            var chars = SpriteAsset.spriteCharacterTable;
            var sb = new System.Text.StringBuilder(chars.Count * 32);
            sb.AppendLine($"[{name}] Atlas mappings ({chars.Count} entries):");

            for (int i = 0; i < chars.Count; i++)
            {
                uint unicode = chars[i].unicode;
                int glyphIdx = (int)chars[i].glyphIndex;
                string emoji = EmojiCodepointHelper.CodepointToDisplayString(unicode);

                sb.AppendLine($"  [{i}] U+{unicode:X4} ({emoji}) → tile {glyphIdx}");
            }

            ReportHub.Log(ReportCategory.CHAT_MESSAGES,sb.ToString());
#endif
        }

        /// <summary>
        /// Converts an array of Unicode codepoints to atlas tile indices.
        /// Logs a warning for any codepoint not found in the atlas.
        /// </summary>
        public int[] ResolveUnicodesToTileIndices(uint[] unicodes)
        {
            var result = new int[unicodes.Length];

            for (int i = 0; i < unicodes.Length; i++)
            {
                result[i] = GetTileIndexFromUnicode(unicodes[i]);

                if (result[i] < 0)
                    ReportHub.LogWarning(ReportCategory.CHAT_MESSAGES, $"Emoji U+{unicodes[i]:X4} not found in atlas — tile index will be -1");
            }

            return result;
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
