using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
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
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using Font = DCL.ECSComponents.Font;

namespace DCL.PluginSystem.World
{
    public class TextShapePlugin : IDCLWorldPlugin<TextShapePlugin.FontsSettings>
    {
        private readonly IPerformanceBudget instantiationFrameTimeBudgetProvider;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        private readonly MaterialPropertyBlock materialPropertyBlock = new ();
        private readonly IComponentPool<TextMeshPro> textMeshProPool;

        private IFontsStorage fontsStorage;

        static TextShapePlugin()
        {
            EntityEventBuffer<TextMeshPro>.Register(1000);
        }

        public TextShapePlugin(IPerformanceBudget instantiationFrameTimeBudgetProvider, CacheCleaner cacheCleaner, IComponentPoolsRegistry componentPoolsRegistry)
        {
            this.instantiationFrameTimeBudgetProvider = instantiationFrameTimeBudgetProvider;
            this.componentPoolsRegistry = componentPoolsRegistry;

            textMeshProPool = componentPoolsRegistry.AddGameObjectPool<TextMeshPro>();
            cacheCleaner.Register(textMeshProPool);
        }

        public void Dispose()
        {
            //ignore
        }

        public UniTask InitializeAsync(FontsSettings settings, CancellationToken ct)
        {
            fontsStorage = settings;
            return new UniTask();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            var buffer = sharedDependencies.EntityEventsBuilder.Rent<TextMeshPro>();

            InstantiateTextShapeSystem.InjectToWorld(ref builder, textMeshProPool, fontsStorage, materialPropertyBlock, instantiationFrameTimeBudgetProvider, buffer);
            UpdateTextShapeSystem.InjectToWorld(ref builder, fontsStorage, materialPropertyBlock, buffer);
            VisibilityTextShapeSystem.InjectToWorld(ref builder, buffer);

            finalizeWorldSystems.RegisterReleasePoolableComponentSystem<TextMeshPro, TextShapeComponent>(ref builder, componentPoolsRegistry);
        }

        [Serializable]
        public class FontsSettings : IDCLPluginSettings, IFontsStorage
        {
            [field: SerializeField]
            public SoFontList FontList { get; private set; }

            public TMP_FontAsset Font(Font font) =>
                FontList!.Font(font);
        }
    }
}
