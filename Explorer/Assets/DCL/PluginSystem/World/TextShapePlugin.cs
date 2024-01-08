using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.TextShape.Fonts;
using DCL.SDKComponents.TextShape.Renderer.Factory;
using DCL.SDKComponents.TextShape.System;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class TextShapePlugin : IDCLWorldPlugin
    {
        private readonly ITextShapeRendererFactory textShapeRendererFactory;
        private readonly IReleasablePerformanceBudget instantiationFrameTimeBudget;

        public TextShapePlugin(IReleasablePerformanceBudget instantiationFrameTimeBudget, IComponentPoolsRegistry componentPoolsRegistry, IPluginSettingsContainer settingsContainer) : this(
            instantiationFrameTimeBudget,
            componentPoolsRegistry,
            settingsContainer.GetSettings<FontsSettings>().AsCached()
        ) { }

        public TextShapePlugin(IReleasablePerformanceBudget instantiationFrameTimeBudget, IComponentPoolsRegistry componentPoolsRegistry, IFontsStorage fontsStorage) : this(
            new PoolTextShapeRendererFactory(componentPoolsRegistry, fontsStorage),
            instantiationFrameTimeBudget
        ) { }

        public TextShapePlugin(ITextShapeRendererFactory textShapeRendererFactory, IReleasablePerformanceBudget instantiationFrameTimeBudget)
        {
            this.textShapeRendererFactory = textShapeRendererFactory;
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
        }

        public void Dispose()
        {
            //ignore
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            InstantiateTextShapeSystem.InjectToWorld(ref builder, textShapeRendererFactory, instantiationFrameTimeBudget);
            UpdateTextShapeSystem.InjectToWorld(ref builder);
            VisibilityTextShapeSystem.InjectToWorld(ref builder);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            InstantiateTextShapeSystem.InjectToWorld(ref builder, textShapeRendererFactory, instantiationFrameTimeBudget);
            UpdateTextShapeSystem.InjectToWorld(ref builder);
            VisibilityTextShapeSystem.InjectToWorld(ref builder);
        }
    }
}
