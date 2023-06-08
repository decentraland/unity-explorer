using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.Textures;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.ECSWorld.Plugins
{
    public class StreamableLoadingPlugin : IECSWorldPlugin
    {
        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            StartLoadingTextureSystem.InjectToWorld(ref builder);
            RepeatTextureLoadingSystem.InjectToWorld(ref builder);
            ConcludeTextureLoadingSystem.InjectToWorld(ref builder, NoCache<Texture2D, GetTextureIntention>.INSTANCE);
            AbortLoadingSystem.InjectToWorld(ref builder);
        }
    }
}
