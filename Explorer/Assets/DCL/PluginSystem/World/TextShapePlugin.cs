using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Fonts;
using DCL.SDKComponents.TextShape.Fonts.Settings;
using DCL.SDKComponents.TextShape.System;
using ECS.Abstract;
using ECS.LifeCycle;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Font = DCL.ECSComponents.Font;

namespace DCL.PluginSystem.World
{
    public class TextShapePlugin : IDCLWorldPlugin<TextShapePlugin.FontsSettings>
    {
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;

        private readonly MaterialPropertyBlock materialPropertyBlock = new ();
        private readonly IComponentPool<TextMeshPro> textMeshProPool;

        private IFontsStorage fontsStorage;

        static TextShapePlugin()
        {
            EntityEventBuffer<TextShapeComponent>.Register(1000);
        }

        public TextShapePlugin(IPerformanceBudget instantiationFrameTimeBudgetProvider, CacheCleaner cacheCleaner, IComponentPoolsRegistry componentPoolsRegistry, IAssetsProvisioner assetsProvisioner)
        {
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.assetsProvisioner = assetsProvisioner;

            textMeshProPool = componentPoolsRegistry.AddGameObjectPool<TextMeshPro>();
            cacheCleaner.Register(textMeshProPool);
        }

        public void Dispose()
        {
            //ignore
        }

        public async UniTask InitializeAsync(FontsSettings settings, CancellationToken ct)
        {
            fontsStorage = (await assetsProvisioner.ProvideMainAssetAsync(settings.FontList, ct)).Value;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var buffer = sharedDependencies.EntityEventsBuilder.Rent<TextShapeComponent>();

            InstantiateTextShapeSystem.InjectToWorld(ref builder, textMeshProPool, fontsStorage, materialPropertyBlock, instantiationFrameTimeBudgetProvider, buffer);
            UpdateTextShapeSystem.InjectToWorld(ref builder, fontsStorage, materialPropertyBlock, buffer, sharedDependencies.SceneData);
            VisibilityTextShapeSystem.InjectToWorld(ref builder, buffer);

            finalizeWorldSystems.RegisterReleasePoolableComponentSystem<TextMeshPro, TextShapeComponent>(ref builder, componentPoolsRegistry);
        }

        [Serializable]
        public class FontsSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceT<SoFontList> FontList { get; private set; }
        }
    }
}
