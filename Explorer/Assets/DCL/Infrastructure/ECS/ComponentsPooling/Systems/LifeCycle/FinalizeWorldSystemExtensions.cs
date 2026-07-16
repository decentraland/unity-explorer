#nullable enable

using Arch.SystemGroups;
using DCL.Optimization.Pools;
using ECS.ComponentsPooling.Systems;
using System.Collections.Generic;

namespace ECS.LifeCycle
{
    public static class FinalizeWorldSystemExtensions
    {
        public static void RegisterReleasePoolableComponentSystem<T, TProvider>(
            this IList<IFinalizeWorldSystem> list,
            ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            IComponentPoolsRegistry componentPoolsRegistry
        ) where TProvider: IPoolableComponentProvider<T> where T: class
        {
            var releasePoolableComponentSystem = ReleasePoolableComponentSystem<T, TProvider>
               .InjectToWorld(ref builder, componentPoolsRegistry);

            list.Add(releasePoolableComponentSystem);
        }
    }
}
