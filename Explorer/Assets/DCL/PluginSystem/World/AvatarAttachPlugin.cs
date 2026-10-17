using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.ECSComponents;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.AvatarAttach.Systems;
using DCL.Utilities;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.PluginSystem.World
{
    public class AvatarAttachPlugin : IDCLWorldPlugin
    {
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly Arch.Core.World globalWorld;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ExposedTransform exposedPlayerTransform;

        public AvatarAttachPlugin(
            Arch.Core.World globalWorld,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            IComponentPoolsRegistry componentPoolsRegistry,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            ExposedTransform exposedPlayerTransform)
        {
            this.globalWorld = globalWorld;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.entityParticipantTable = entityParticipantTable;
            this.exposedPlayerTransform = exposedPlayerTransform;
        }

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            InstantiateTransformForAvatarAttachSystem.InjectToWorld(ref builder, componentPoolsRegistry.GetReferenceTypePool<Transform>(), persistentEntities.SceneRoot);

            ResetDirtyFlagSystem<PBAvatarAttach>.InjectToWorld(ref builder);

            var avatarShapeHandlerSystem = AvatarAttachHandlerSystem.InjectToWorld(ref builder,
                globalWorld,
                mainPlayerAvatarBaseProxy,
                exposedPlayerTransform,
                sharedDependencies.SceneStateProvider,
                entityParticipantTable,
                sharedDependencies.EcsToCRDTWriter);

            finalizeWorldSystems.Add(avatarShapeHandlerSystem);

            AvatarAttachHandlerSetupSystem.InjectToWorld(ref builder,
                globalWorld,
                mainPlayerAvatarBaseProxy,
                sharedDependencies.SceneStateProvider,
                entityParticipantTable);
        }
    }
}
