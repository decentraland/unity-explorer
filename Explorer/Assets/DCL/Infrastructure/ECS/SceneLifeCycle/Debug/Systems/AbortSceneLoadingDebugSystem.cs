using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Linq;
using UnityEngine;

namespace ECS.SceneLifeCycle.Debug
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(LoadSceneSystem))]
    [LogCategory(ReportCategory.DEBUG)]
    public partial class AbortSceneLoadingDebugSystem : BaseUnityLoopSystem
    {
        private readonly EnumElementBinding<SceneAbortKind> abortKind;
        private readonly ElementBinding<Vector2Int> coords;

        private AbortSceneLoadingDebugSystem(World world, DebugWidgetBuilder debugWidgetBuilder) : base(world)
        {
            debugWidgetBuilder.AddControl(new DebugDropdownDef(abortKind = new EnumElementBinding<SceneAbortKind>(SceneAbortKind.NONE), "Type"),
                null,
                debugHintDef: new DebugHintDef("Abort Scene Loading"));

            debugWidgetBuilder.AddControl(new DebugVector2IntFieldDef(coords = new ElementBinding<Vector2Int>(Vector2Int.zero)), null);
        }

        protected override void Update(float t)
        {
            if (abortKind.Value == SceneAbortKind.NONE)
                return;

            TryAbortLoadingQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<ISceneFacade>))]
        private void TryAbortLoading(Entity entity, in GetSceneFacadeIntention intention)
        {
            if (!intention.DefinitionComponent.Contains(coords.Value)) return;

            Exception exceptionToReport = abortKind.Value switch
                                          {
                                              SceneAbortKind.CANCEL => new OperationCanceledException(),
                                              _ => new Exception($"Loading of Scene {intention.DefinitionComponent.Definition.metadata.scene.DecodedBase} has been manually interrupted"),
                                          };

            World.Add(entity, new StreamableLoadingResult<ISceneFacade>(GetReportData(), exceptionToReport));
        }
    }
}
