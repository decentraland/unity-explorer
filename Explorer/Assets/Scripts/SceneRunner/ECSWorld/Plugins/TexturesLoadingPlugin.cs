using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.StreamableLoading.Textures;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.ECSWorld.Plugins
{
    public class TexturesLoadingPlugin : IECSWorldPlugin
    {
        private readonly IConcurrentBudgetProvider concurrentLoadingBudgetProvider;

        public TexturesLoadingPlugin(IConcurrentBudgetProvider concurrentLoadingBudgetProvider)
        {
            this.concurrentLoadingBudgetProvider = concurrentLoadingBudgetProvider;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            LoadTextureSystem.InjectToWorld(ref builder, NoCache<Texture2D, GetTextureIntention>.INSTANCE, sharedDependencies.MutexSync);
        }
    }
}
