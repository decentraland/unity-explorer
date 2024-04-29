using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using DCL.SDKComponents.TextShape.Renderer;
using DCL.SDKComponents.TextShape.Renderer.Factory;
using DCL.SDKComponents.TextShape.System;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class TextShapePlugin : IDCLWorldPlugin
    {
        private readonly ITextShapeRendererFactory textShapeRendererFactory;
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public TextShapePlugin(IPerformanceBudget instantiationFrameTimeBudgetProvider, IComponentPoolsRegistry componentPoolsRegistry, IPluginSettingsContainer settingsContainer) : this(
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry,
            settingsContainer.GetSettings<FontsSettings>().AsCached()
        ) { }

        public TextShapePlugin(IPerformanceBudget instantiationFrameTimeBudgetProvider, IComponentPoolsRegistry componentPoolsRegistry, IFontsStorage fontsStorage) : this(
            new PoolTextShapeRendererFactory(componentPoolsRegistry, fontsStorage),
            instantiationFrameTimeBudgetProvider,
            componentPoolsRegistry
        ) { }

        public TextShapePlugin(ITextShapeRendererFactory textShapeRendererFactory, IPerformanceBudget instantiationFrameTimeBudgetProvider, IComponentPoolsRegistry componentPoolsRegistry)
        {
            this.textShapeRendererFactory = textShapeRendererFactory;
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.componentPoolsRegistry = componentPoolsRegistry;
        }

        public void Dispose()
        {
            //ignore
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            InstantiateTextShapeSystem.InjectToWorld(ref builder, textShapeRendererFactory, instantiationFrameTimeBudgetProvider);
            UpdateTextShapeSystem.InjectToWorld(ref builder);
            VisibilityTextShapeSystem.InjectToWorld(ref builder);
            finalizeWorldSystems.RegisterReleasePoolableComponentSystem<ITextShapeRenderer, TextShapeRendererComponent>(ref builder, componentPoolsRegistry);
        }
    }
}
