using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    internal static class ChatReactionMaterialFactory
    {
        public static Material CreateRuntimeMaterial(ChatReactionsConfig config, string suffix = "Runtime")
        {
            var mat = new Material(config.EmojiMaterial)
            {
                name = $"{config.EmojiMaterial.name} ({suffix})"
            };

            ChatReactionsAtlasHelper.ApplyAtlasToMaterial(mat, config);
            return mat;
        }
    }
}
