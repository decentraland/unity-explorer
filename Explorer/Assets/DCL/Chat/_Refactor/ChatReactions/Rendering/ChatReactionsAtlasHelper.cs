using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Rendering
{
    /// <summary>
    /// Shared utility for applying atlas settings to a runtime material.
    /// Used by both <see cref="ChatReactionUISimulation"/> and <see cref="ChatReactionWorldSimulation"/>.
    /// </summary>
    public static class ChatReactionsAtlasHelper
    {
        private static readonly int AtlasTexId  = Shader.PropertyToID("_AtlasTex");
        private static readonly int AtlasColsId = Shader.PropertyToID("_AtlasCols");
        private static readonly int AtlasRowsId = Shader.PropertyToID("_AtlasRows");
        public static void ApplyAtlasToMaterial(Material mat, ChatReactionsConfig config)
        {
            if (mat == null || config.Atlas == null) return;

            mat.SetTexture(AtlasTexId, config.Atlas.Atlas);
            mat.SetFloat(AtlasColsId, config.Atlas.Cols);
            mat.SetFloat(AtlasRowsId, config.Atlas.Rows);
        }
    }
}
