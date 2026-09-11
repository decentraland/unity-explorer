using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace ECS.Unity.ExplorerUiEvents
{
    /// <summary>
    ///     Forwards the panel life cycle events the scene's own <c>openExplorerUi</c> calls produced to that
    ///     scene, as a grow-only <see cref="PBExplorerUiEventsResult" /> set on the scene root entity.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateBefore(typeof(CleanUpGroup))]
    public partial class WriteExplorerUiEventsSystem : BaseUnityLoopSystem
    {
        private readonly Queue<ExplorerUiEvent> events;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;

        internal WriteExplorerUiEventsSystem(World world, Queue<ExplorerUiEvent> events, IECSToCRDTWriter ecsToCRDTWriter, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.events = events;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            var tickNumber = (int)sceneStateProvider.TickNumber;

            while (events.TryDequeue(out ExplorerUiEvent uiEvent))
            {
                ecsToCRDTWriter.AppendMessage<PBExplorerUiEventsResult, (ExplorerUiEvent uiEvent, uint timestamp)>(static (result, data) =>
                {
                    result.Ui = data.uiEvent.Ui;
                    result.Timestamp = data.timestamp;

                    switch (data.uiEvent.Kind)
                    {
                        case ExplorerUiEventKind.Opened:
                            result.Opened = new PBExplorerUiEventsResult.Types.UiOpened();
                            break;
                        case ExplorerUiEventKind.Closed:
                            result.Closed = new PBExplorerUiEventsResult.Types.UiClosed();
                            break;
                    }
                }, SpecialEntitiesID.SCENE_ROOT_ENTITY, tickNumber, (uiEvent, (uint)tickNumber));
            }
        }
    }
}
