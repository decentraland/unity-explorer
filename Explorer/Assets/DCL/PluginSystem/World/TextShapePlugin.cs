using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.World.Dependencies;
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

        public TextShapePlugin() : this(new TextShapeRendererFactory()) { }

        public TextShapePlugin(ITextShapeRendererFactory textShapeRendererFactory)
        {
            this.textShapeRendererFactory = textShapeRendererFactory;
        }

        public void Dispose()
        {
            //ignore
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            InstantiateTextShapeSystem.InjectToWorld(ref builder, textShapeRendererFactory);
            UpdateTextShapeSystem.InjectToWorld(ref builder);
            VisibilityTextShapeSystem.InjectToWorld(ref builder);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies)
        {
            InstantiateTextShapeSystem.InjectToWorld(ref builder, textShapeRendererFactory);
            UpdateTextShapeSystem.InjectToWorld(ref builder);
            VisibilityTextShapeSystem.InjectToWorld(ref builder);
        }
    }
}
