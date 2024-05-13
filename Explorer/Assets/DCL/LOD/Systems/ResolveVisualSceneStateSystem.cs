using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.LOD;
using DCL.LOD.Systems;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    [UpdateBefore(typeof(UpdateVisualSceneStateSystem))]
    [UpdateBefore(typeof(UpdateSceneLODInfoSystem))]
    public partial class ResolveVisualSceneStateSystem : BaseUnityLoopSystem
    {
        private readonly ILODSettingsAsset lodSettingsAsset;
        private readonly VisualSceneStateResolver visualSceneStateResolver;
        private readonly IRealmData realmData;

        public ResolveVisualSceneStateSystem(World world, ILODSettingsAsset lodSettingsAsset, VisualSceneStateResolver visualSceneStateResolver, IRealmData realmData) : base(world)
        {
            this.lodSettingsAsset = lodSettingsAsset;
            this.visualSceneStateResolver = visualSceneStateResolver;
            this.realmData = realmData;
        }

        protected override void Update(float t)
        {
            AddSceneVisualStateQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(VisualSceneState))]
        private void AddSceneVisualState(in Entity entity, ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            VisualSceneState visualSceneState = new VisualSceneState();
            visualSceneStateResolver.ResolveVisualSceneState(ref visualSceneState, partition, sceneDefinitionComponent, lodSettingsAsset, realmData);
            visualSceneState.IsDirty = false;
            World.Add(entity, visualSceneState);
        }
    }
}
