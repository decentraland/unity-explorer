using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Shared utility for applying atlas settings to a runtime material.
    /// Used by both <see cref="ChatReactionSimulation"/> and <see cref="ChatReactionWorldSimulation"/>.
    /// </summary>
    public static class ChatReactionsAtlasHelper
    {
        private static readonly int AtlasTexId  = Shader.PropertyToID("_AtlasTex");
        private static readonly int AtlasColsId = Shader.PropertyToID("_AtlasCols");
        private static readonly int AtlasRowsId = Shader.PropertyToID("_AtlasRows");
        private static readonly int FlipYId     = Shader.PropertyToID("_FlipY");

        public static void ApplyAtlasToMaterial(Material mat, ChatReactionsSituationalConfig config)
        {
            if (mat == null || config.Atlas == null) return;

            mat.SetTexture(AtlasTexId, config.Atlas.Atlas);
            mat.SetFloat(AtlasColsId, config.Atlas.Cols);
            mat.SetFloat(AtlasRowsId, config.Atlas.Rows);
            mat.SetFloat(FlipYId, config.Atlas.FlipY ? 1f : 0f);
        }
    }
}
