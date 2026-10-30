using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
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

        internal WriteExplorerUiEventsSystem(World world, Queue<ExplorerUiEvent> events, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.events = events;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            while (events.TryDequeue(out ExplorerUiEvent uiEvent))
            {
                ecsToCRDTWriter.AppendMessage<PBExplorerUiEventsResult, ExplorerUiEvent>(static (result, data) =>
                {
                    result.Ui = data.Ui;
                    result.Timestamp = data.Tick;
                    result.RequestId = data.RequestId;

                    switch (data.Kind)
                    {
                        case ExplorerUiEventKind.Opened:
                            result.Opened = new PBExplorerUiEventsResult.Types.UiOpened();
                            break;
                        case ExplorerUiEventKind.Closed:
                            result.Closed = new PBExplorerUiEventsResult.Types.UiClosed();
                            break;
                    }
                }, SpecialEntitiesID.SCENE_ROOT_ENTITY, (int)uiEvent.Tick, uiEvent);
            }
        }
    }
}
