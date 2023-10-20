using Arch.SystemGroups;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.StreamableLoading.Textures;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class TexturesLoadingPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly MemoryBudgetProvider memoryBudgetProvider;

        public TexturesLoadingPlugin(MemoryBudgetProvider memoryBudgetProvider)
        {
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            LoadTextureSystem.InjectToWorld(ref builder, memoryBudgetProvider, NoCache<Texture2D, GetTextureIntention>.INSTANCE, sharedDependencies.MutexSync);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
