using UnityEngine;

namespace DCL.Chat.Reactions
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

        public int Cols => Atlas != null ? Mathf.Max(1, Atlas.width / TileSizePx) : 1;
        public int Rows => Atlas != null ? Mathf.Max(1, Atlas.height / TileSizePx) : 1;
        public int TotalTiles => Cols * Rows;
    }
}
