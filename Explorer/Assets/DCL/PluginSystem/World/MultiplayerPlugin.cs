using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DCL.MCP.Systems;
using DCL.Multiplayer.SDK.Systems.SceneWorld;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.SceneLifeCycle;
using System.Collections.Generic;
using WriteAvatarEmoteCommandSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteAvatarEmoteCommandSystem;
using WriteAvatarEquippedDataSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteAvatarEquippedDataSystem;
using WritePlayerIdentityDataSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WritePlayerIdentityDataSystem;
using WriteSDKAvatarBaseSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteSDKAvatarBaseSystem;

namespace DCL.PluginSystem.World
{
    public class MultiplayerPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly Arch.Core.World globalWorld;
        private readonly Arch.Core.Entity globalPlayerEntity;
        private readonly IScenesCache scenesCache;
        private readonly IComponentPool<SDKTransform> sdkTransformPool;
        private readonly IComponentPool<PBTextShape> textShapePool;
        private readonly IComponentPool<PBMeshRenderer> meshRendererPool;
        private readonly IComponentPool<PBMeshCollider> colliderPool;
        private readonly IComponentPool<PBGltfContainer> gltfPool;

        public MultiplayerPlugin(Arch.Core.World globalWorld, Entity globalPlayerEntity, IComponentPoolsRegistry componentPoolsRegistry,
            IScenesCache scenesCache)
        {
            this.globalWorld = globalWorld;
            this.globalPlayerEntity = globalPlayerEntity;
            this.scenesCache = scenesCache;
            sdkTransformPool = componentPoolsRegistry.GetReferenceTypePool<SDKTransform>();
            textShapePool = componentPoolsRegistry.GetReferenceTypePool<PBTextShape>();
            meshRendererPool = componentPoolsRegistry.GetReferenceTypePool<PBMeshRenderer>();
            colliderPool = componentPoolsRegistry.GetReferenceTypePool<PBMeshCollider>();
            gltfPool = componentPoolsRegistry.GetReferenceTypePool<PBGltfContainer>();
        }

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            WritePlayerIdentityDataSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteSDKAvatarBaseSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteAvatarEquippedDataSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteAvatarEmoteCommandSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, sharedDependencies.SceneStateProvider);
            WritePlayerTransformSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, sharedDependencies.SceneData);
            MCPSceneCreationSystem.InjectToWorld(ref builder, globalWorld, globalPlayerEntity, scenesCache, sharedDependencies.EcsToCRDTWriter, sharedDependencies.EntitiesMap, sdkTransformPool, textShapePool, meshRendererPool, colliderPool, gltfPool);

            CleanUpAvatarPropagationComponentsSystem.InjectToWorld(ref builder);
        }
    }
}
