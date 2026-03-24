using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Play;
using DCL.Multiplayer.Emotes;
using DCL.PluginSystem.World.Dependencies;
using DCL.Utilities;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class SceneMaskedEmotePlugin : IDCLWorldPlugin
    {
        private readonly Arch.Core.World globalWorld;
        private readonly Arch.Core.Entity globalPlayerEntity;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly ObjectProxy<EmotePlayer> emotePlayerProxy;
        private readonly ObjectProxy<IEmoteStorage> emoteStorageProxy;
        private readonly ObjectProxy<IEmotesMessageBus> messageBusProxy;

        public SceneMaskedEmotePlugin(
            Arch.Core.World globalWorld,
            Arch.Core.Entity globalPlayerEntity,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            ObjectProxy<EmotePlayer> emotePlayerProxy,
            ObjectProxy<IEmoteStorage> emoteStorageProxy,
            ObjectProxy<IEmotesMessageBus> messageBusProxy)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.emotePlayerProxy = emotePlayerProxy;
            this.emoteStorageProxy = emoteStorageProxy;
            this.messageBusProxy = messageBusProxy;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var system = SceneMaskedEmoteSystem.InjectToWorld(ref builder,
                globalWorld,
                globalPlayerEntity,
                mainPlayerAvatarBaseProxy,
                emotePlayerProxy,
                emoteStorageProxy,
                messageBusProxy,
                sharedDependencies.SceneStateProvider);

            finalizeWorldSystems.Add(system);
        }
    }
}
