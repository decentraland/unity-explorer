using Arch.Core;
using DCL.AvatarRendering.Emotes;

namespace DCL.PluginSystem.Global
{
    public readonly struct GlobalPluginArguments
    {
        /// <summary>
        ///     Player entity is persistent and never dies
        /// </summary>
        public readonly Entity PlayerEntity;

        public readonly IEmoteProvider EmoteProvider;

        public GlobalPluginArguments(Entity playerEntity, IEmoteProvider emoteProvider)
        {
            PlayerEntity = playerEntity;
            EmoteProvider = emoteProvider;
        }
    }
}
