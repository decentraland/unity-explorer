using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.Textures;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class PromisesAnalyticsPlugin : IDCLWorldPluginWithoutSettings, IDCLGlobalPluginWithoutSettings
    {
        private readonly IDebugContainerBuilder debugContainerBuilder;

        private readonly CountPromisesDebugSystem.ISubQuery[]? subQueries;

        public PromisesAnalyticsPlugin(IDebugContainerBuilder debugContainerBuilder)
        {
            this.debugContainerBuilder = debugContainerBuilder;

            DebugWidgetBuilder? widget = this.debugContainerBuilder.TryAddWidget("Promises");

            subQueries = new[]
            {
                CreateSubQuery<GetAssetBundleIntention>(widget),
                CreateSubQuery<GetTextureIntention>(widget),
                CreateSubQuery<GetGLTFIntention>(widget),
            };
        }

        public void Dispose() { }

        private CountPromisesDebugSystem.ISubQuery CreateSubQuery<TIntent>(DebugWidgetBuilder? widgetBuilder) where TIntent: ILoadingIntention
        {
            var subQuery = new CountPromisesDebugSystem.SubQuery<TIntent>();
            widgetBuilder?.AddMarker($"{typeof(TIntent).Name}-TTA", subQuery.TimeToAllowAverage.averageNs, DebugLongMarkerDef.Unit.TimeNanoseconds);
            return subQuery;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            CountPromisesDebugSystem.InjectToWorld(ref builder, subQueries);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            CountPromisesDebugSystem.InjectToWorld(ref builder, subQueries);
        }

        public UniTask InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
