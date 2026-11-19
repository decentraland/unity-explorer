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
    ///     scene, as a grow-only <see cref="PBExplorerUiEventsResult" /> set on the scene root entity, and the
    ///     outcomes of its item purchases as a grow-only <see cref="PBExplorerItemPurchaseResult" /> set.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateBefore(typeof(CleanUpGroup))]
    public partial class WriteExplorerUiEventsSystem : BaseUnityLoopSystem
    {
        private readonly Queue<ExplorerUiEvent> events;
        private readonly Queue<ExplorerItemPurchaseEvent> itemPurchaseEvents;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        internal WriteExplorerUiEventsSystem(World world, Queue<ExplorerUiEvent> events, Queue<ExplorerItemPurchaseEvent> itemPurchaseEvents, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.events = events;
            this.itemPurchaseEvents = itemPurchaseEvents;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            // Purchase outcomes go out first: an outcome is queued before the Closed event of its modal,
            // so this order never writes a Closed ahead of the outcome it ends.
            while (itemPurchaseEvents.TryDequeue(out ExplorerItemPurchaseEvent purchaseEvent))
            {
                ecsToCRDTWriter.AppendMessage<PBExplorerItemPurchaseResult, ExplorerItemPurchaseEvent>(static (result, data) =>
                {
                    result.Urn = data.Urn;
                    result.Timestamp = data.Tick;
                    result.RequestId = data.RequestId;

                    switch (data.Status)
                    {
                        case ExplorerItemPurchaseStatus.Purchased:
                            result.Purchased = new PBExplorerItemPurchaseResult.Types.Purchased();
                            break;
                        case ExplorerItemPurchaseStatus.Dismissed:
                            result.Dismissed = new PBExplorerItemPurchaseResult.Types.Dismissed();
                            break;
                        case ExplorerItemPurchaseStatus.Failed:
                            result.Failed = new PBExplorerItemPurchaseResult.Types.Failed();
                            break;
                    }
                }, SpecialEntitiesID.SCENE_ROOT_ENTITY, (int)purchaseEvent.Tick, purchaseEvent);
            }

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
