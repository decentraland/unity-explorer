using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using DCL.SDKComponents.TextShape.System;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class TextShapePlugin : IDCLWorldPlugin
    {
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IFontsStorage fontsStorage;

        private readonly MaterialPropertyBlock materialPropertyBlock = new ();

        public TextShapePlugin(IPerformanceBudget instantiationFrameTimeBudgetProvider, IComponentPoolsRegistry componentPoolsRegistry, IPluginSettingsContainer settingsContainer)
        {
            fontsStorage = settingsContainer.GetSettings<FontsSettings>().AsCached();
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.componentPoolsRegistry = componentPoolsRegistry;

            componentPoolsRegistry.AddGameObjectPool<TextMeshPro>();
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            IComponentPool<TextMeshPro> textMeshProPool = componentPoolsRegistry.GetReferenceTypePool<TextMeshPro>();

            InstantiateTextShapeSystem.InjectToWorld(ref builder, textMeshProPool, fontsStorage, materialPropertyBlock, instantiationFrameTimeBudgetProvider);
            UpdateTextShapeSystem.InjectToWorld(ref builder, fontsStorage, materialPropertyBlock);
            VisibilityTextShapeSystem.InjectToWorld(ref builder);

            finalizeWorldSystems.RegisterReleasePoolableComponentSystem<TextMeshPro, TextShapeComponent>(ref builder, componentPoolsRegistry);
        }
    }
}
